# MEMORY.md -- Codebase Reference for Agents

This document is a structured map of the `blue-nile-pds` codebase. Read this first to orient yourself before making changes.

---

## 1. Project Overview

`blue-nile-pds` is a .NET 10 preview C# implementation of an ATProto Personal Data Server (PDS). It is a fork of `atompds`, experimental and learning-focused. It hosts user accounts, stores repos (Merkle Search Trees), serves blobs, sequences events to a firehose, and exposes ~63 `com.atproto.*` XRPC endpoints plus ~35 AppView proxy endpoints, OAuth, and well-known endpoints.

- **SDK:** .NET 10 (`10.0.100`, pinned in `global.json`, `rollForward: latestMinor`, test runner: `Microsoft.Testing.Platform`)
- **Test framework:** TUnit (NOT xUnit despite what AGENTS.md says)
- **Solution file:** `atompds.slnx` (XML-based slnx format)
- **Central package management:** `Directory.Packages.props` (CPM)
- **Build props:** `Directory.Build.props` (applies `Microsoft.VisualStudio.Threading.Analyzers` to all projects)

---

## 2. Solution Structure

```
atompds.slnx
├── src/atompds/              ASP.NET Core host (web app entry point)
├── src/pdsadmin/              Admin CLI tool (ConsoleAppFramework)
├── src/migration/             Batch actor store migration utility (DurableTask)
├── src/pds_projects/          PDS-specific service libraries
│   ├── AccountManager/        Account CRUD, auth tokens, invites, passwords
│   ├── ActorStore/            Per-actor repo storage (SQLite per DID)
│   ├── BlobStore/             Blob storage (disk or S3)
│   ├── Config/                Configuration record types
│   ├── Mailer/                Email sending (SMTP or stub)
│   ├── Sequencer/             Event sequencing & firehose
│   └── Xrpc/                  XRPC error/response types
├── src/projects/              Lower-level shared libraries
│   ├── CID/                   Content Identifier (v0/v1, CBOR, multihash)
│   ├── Common/                TID, RecordKey, CborBlock, ICborEncodable<T>
│   ├── CommonDb/              Shared EF Core SQLite package ref (no source)
│   ├── CommonWeb/             DID doc models, validation, CarpaNet lexicon types
│   ├── Crypto/                Secp256k1 keypairs, DID key plugins, multibase
│   ├── DidLib/                PLC operations, DID creation/signing
│   ├── Handle/                Handle validation, normalization, slur detection
│   ├── Identity/              DID/handle resolution (PLC, web, DNS), caching
│   └── Repo/                  Merkle Search Tree, CAR files, commit lifecycle
└── test/
    ├── CID.Tests/             CID parsing, creation, round-trip (12 tests)
    ├── Common.Tests/          TID, S32 encoding, CBOR round-trip (10 tests)
    ├── ActorStore.Tests/      `Prepare.ExtractBlobReferences` — blob reference extraction from JSON (156 tests)
    ├── SubscribeTester/       Manual WebSocket subscription test tool
    ├── data/                  Shared test data (blob files)
    ├── ActorStore.Test/       (remnant — no .csproj or source files)
    └── Common.Test/           (remnant — no .csproj or source files)
```

---

## 3. Dependency Graph

```
Crypto (leaf)              CID (leaf)
  │                          │
  ├─ DidLib                  ├─ Common
  │    └─ needs IKeyPair     │    ├─ ICborEncodable<T>
  │                          │    ├─ TID, RecordKey
  ├─ Identity                │    └─ CborBlock
  │    └─ DidResolver        │
  │    └─ HandleResolver     ├─ DidLib ──── Crypto
  │                          │
  └─ Repo                    ├─ Repo ────── Crypto, CID, CommonWeb
       └─ MST, CAR, commits  │
                             ├─ CommonWeb ── Xrpc (pds_projects)
                             │    └─ DidDocument, Util, CarpaNet types
                             │
                             └─ Handle ──── Identity, Config, Xrpc

pds_projects layer:
  Config ←─ AccountManager ←─ ActorStore ←─ BlobStore
          ←─ Sequencer
          ←─ Mailer
          ←─ Xrpc (error types used by nearly everything)

atompds host references ALL pds_projects + Repo
```

---

## 4. Key Entry Points

### Web Host: `src/atompds/Program.cs`

1. Reads `Config` section from appsettings → `ServerEnvironment`
2. Creates `ServerConfig` (validates env, expands paths, maps sub-configs)
3. `ServerConfig.RegisterServices()` wires all DI (see DI section below)
4. Auto-migrates `AccountManagerDb` and `SequencerDb` on startup
5. Registers `BackgroundJobQueue` (singleton) + `BackgroundJobWorker` (hosted service)
6. Configures JSON serialization (ignore defaults), HTTP logging, XRPC exception handler
7. Enables rate limiting (`AddPdsRateLimiting`) when `PDS_RATE_LIMITS_ENABLED` is true
8. Middleware pipeline: routing → rate limiter → map controllers → exception handler → auth middleware → not-found middleware → WebSockets → CORS
9. Static endpoints: `/` (server info JSON), `/robots.txt`, `/tls-check`

### Config: `src/atompds/Config/ServerEnvironment.cs`

All config is bound from `appsettings.Development.json` `Config` section. Key required fields: `PDS_JWT_SECRET`, `PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX`. See `appsettings.Development.json.example`.

### DI Wiring: `src/atompds/Config/ServerConfig.cs`

`RegisterServices()` registers:
- All config sub-records as singletons
- `AccountManagerDb` (scoped DbContext, SQLite)
- `SequencerDb` (DbContextFactory, SQLite)
- `AccountRepository`, `AccountStore`, `PasswordStore`, `RepoStore`, `InviteStore`, `EmailTokenStore`, `AppPasswordStore`, `Auth` (all scoped)
- `ActorRepositoryProvider` (scoped, creates per-actor repos)
- `BlobStoreFactory` (singleton)
- `IdResolver`, `HandleManager`, `IDidCache` → `MemoryCache` (singleton)
- `AuthVerifier`, `ServiceJwtBuilder` (scoped)
- `AuthVerifierConfig` (singleton)
- `SequencerRepository` (scoped), `Crawlers` (singleton), `CrawlersConfig` (singleton)
- `PlcClient`, `PlcClientConfig` (singleton)
- `IMailer` → `SmtpMailer` or `StubMailer` based on SMTP config (singleton)
- `CaptchaVerifier` (singleton, for hCaptcha token verification)
- `WriteSnapshotCache` (singleton, read-after-write consistency for records)
- `OAuthSessionStore` (singleton, in-memory OAuth PKCE flow state)
- `IScratchCache` → `MemoryScratchCache` (singleton, ephemeral key-value cache)
- `BackgroundJobQueue` (singleton) + `BackgroundJobWorker` (hosted service, bounded channel)

---

## 5. Controller Architecture

### Location & Naming

All XRPC controllers live under `src/atompds/Controllers/Xrpc/Com/Atproto/` organized by namespace. Additional controllers exist under `Controllers/OAuth/`, `Controllers/WellKnownController.cs`, `Controllers/ErrorController.cs`, and `Controllers/Xrpc/AppViewProxyController.cs`.

| Namespace | Directory | Endpoints | Notes |
|-----------|-----------|-----------|-------|
| `com.atproto.admin.*` | `Admin/` | 11 | `AccountInvitesAdminController` has enable+disable (2), `SubjectStatusController` has get+update (2) |
| `com.atproto.identity.*` | `Identity/` | 6 | |
| `com.atproto.repo.*` | `Repo/` | 8 | `ApplyWritesController` has 5 endpoints (getRecord, putRecord, deleteRecord, createRecord, applyWrites) |
| `com.atproto.server.*` | `Server/` | 25 | `DeleteAccountController` has requestAccountDelete + deleteAccount (2) |
| `com.atproto.sync.*` | `Sync/` | 11 | Includes `listMissingBlobs`, `importRepo` |
| `com.atproto.moderation.*` | `Moderation/` | 1 | `createReport` |
| `com.atproto.temp.*` | `Temp/` | 1 | `checkSignupQueue` |
| `app.bsky.*` + `chat.bsky.*` | `AppViewProxyController.cs` | 33+ specific + 2 catch-all | Proxies to Bsky AppView |
| (health) | `HealthController.cs` | 1 | `health` |
| (OAuth) | `Controllers/OAuth/` | 4 | `authorize` (GET+POST consent), `token`, `client-metadata.json` |
| (well-known) | `WellKnownController.cs` | 3 | `oauth-protected-resource`, `oauth-authorization-server`, `atproto-did` |
| (error) | `ErrorController.cs` | — | Exception handler target |

### Controller Pattern

```csharp
[ApiController]
[Route("xrpc")]
public class XxxController : ControllerBase
{
    // Constructor-injected dependencies
    private readonly SomeService _service;
    
    [HttpGet("com.atproto.namespace.method")]
    [AccessStandard]  // or [AdminToken], [AccessPrivileged], [AccessFull], [Refresh]
    public async Task<IActionResult> MethodAsync([FromQuery] string param)
    {
        var auth = HttpContext.GetAuthOutput(); // extract auth when needed
        // validate input, throw XRPCError on failure
        // call services
        return Ok(new { ... });
    }
}
```

### Auth Attributes (defined in `AuthMiddleware.cs`)

| Attribute | Meaning | Typical Use |
|-----------|---------|-------------|
| `[AdminToken]` | Basic auth admin:password | Admin endpoints |
| `[AccessStandard]` | Accepts access + appPass + appPassPrivileged JWTs | Record writes, reads |
| `[AccessFull]` | Accepts only access JWT (no app passwords) | Account deletion, deactivation |
| `[AccessPrivileged]` | Accepts access + appPassPrivileged JWTs | Handle update, invite codes |
| `[Refresh]` | Requires refresh JWT | Session refresh/delete |

Attributes take optional `(bool checkTakenDown, bool checkDeactivated)` params.

### Error Handling

- Throw `XRPCError(new XxxErrorDetail("message"))` for API errors
- Error detail types: `InvalidRequestErrorDetail`, `AuthRequiredErrorDetail`, `InvalidTokenErrorDetail`, `ExpiredTokenErrorDetail`, `InvalidInviteCodeErrorDetail`, `HandleNotAvailableErrorDetail`, `AccountTakenDownErrorDetail`, etc.
- `XRPCExceptionHandler` converts `XRPCError` to HTTP responses
- All error types defined in `src/pds_projects/Xrpc/Errors.cs`

---

## 6. Core Subsystems

### 6a. Actor Store (`src/pds_projects/ActorStore/`)

Each user gets their own SQLite database at `<actor_dir>/<sha256(did)>/store.sqlite`.

**Key classes:**
- `ActorRepositoryProvider` — factory: `Open(did)`, `Create(did, keyPair)`, `Destroy(did)`, `Exists(did)`
- `ActorRepository` — unit-of-work facade wrapping `ActorStoreDb` + sub-repositories: `Repo`, `Record`, blobs
- `SqlRepoTransactor` — implements `IRepoStorage`, bridges MST layer to SQLite blocks table
- `RepoRepository` — `CreateRepoAsync`, `ProcessWritesAsync`, `FormatCommitAsync`
- `RecordRepository` — `GetRecordAsync`, `IndexRecordAsync`, `DeleteRecordAsync`, `ListRecordsForCollectionAsync`
- `BlobTransactor` — blob lifecycle (temp→permanent, deref cleanup)
- `Prepare` — static helpers: `PrepareCreate`, `PrepareUpdate`, `PrepareDelete`, `ExtractBlobReferences`

**DB tables (per actor):** `repo_root`, `repo_block`, `record`, `backlink`, `blob`, `record_blob`, `account_pref`

**Migrations:** `20241207005453_Init`, `20260112165417_BlobStatus`

### 6b. Account Manager (`src/pds_projects/AccountManager/`)

Global SQLite database (`account.sqlite`) shared across all accounts.

**Key classes:**
- `AccountRepository` — facade composing `AccountStore`, `PasswordStore`, `RepoStore`, `InviteStore`, `EmailTokenStore`, `AppPasswordStore`, `Auth`
- `Auth` — JWT creation/validation, refresh token rotation, token storage

**Store classes** (all in `Db/`): `AccountStore`, `PasswordStore`, `RepoStore`, `InviteStore`, `EmailTokenStore`, `AppPasswordStore`

**DB models:** `ActorAccount`, `RefreshToken`, plus EF entities for each store.

### 6c. Sequencer (`src/pds_projects/Sequencer/`)

Event store for the firehose. Global SQLite (`sequencer.sqlite`).

**Key classes:**
- `SequencerRepository` — `SequenceCommitAsync`, `SequenceHandleUpdateAsync`, `SequenceIdentityEventAsync`, `SequenceAccountEventAsync`, `SequenceTombstoneEventAsync`, `SequenceEventAsync`, `GetRangeAsync`. Background polling with `OnEvents` event.
- `Outbox` — bounded `Channel<ISeqEvt>` fan-out to WebSocket consumers. Supports backfill + live cutover.
- `Crawlers` — rate-limited notification to relay/BGS hosts (POST `requestCrawl`)

**Event types:** `CommitEvt`, `HandleEvt`, `IdentityEvt`, `AccountEvt`, `TombstoneEvt` (all `ICborEncodable<T>`, stored as CBOR `byte[]`). Typed wrappers `TypedCommitEvt`, `TypedHandleEvt`, `TypedIdentityEvt`, `TypedAccountEvt`, `TypedTombstoneEvt` implement `ISeqEvt` (adds `Seq`, `Time`). `CommitEvtOp` holds per-operation action/path/cid.

**DB table:** `RepoSeqs` (Seq auto-inc PK, Did, EventType, Event bytes, Invalidated, SequencedAt)

### 6d. Blob Store (`src/pds_projects/BlobStore/`)

- `BlobStoreFactory` — creates `DiskBlobStore` or `S3BlobStore` per config
- `DiskBlobStore` — file system: `<location>/<did>/<cid>` permanent, `<tmp>/<did>/<key>` temp
- `S3BlobStore` — AWS S3: `blocks/<did>/<cid>` permanent, `tmp/<did>/<key>` temp

### 6e. Repo / MST (`src/projects/Repo/`)

- `Repo` — core class: `CreateAsync`, `LoadAsync`, `FormatCommitAsync`, `ApplyWritesAsync`
- `MST` — Merkle Search Tree: `AddAsync`, `DeleteAsync`, `GetAsync`, `WalkAsync`, `SplitAroundAsync`
- `MSTDiff` — dual-walker diff: produces `DataDiff` (adds/updates/deletes)
- `CarEncoder` / `CarMemoryWriter` — CAR v1 file encoding
- `BlockMap` — CID→bytes dictionary
- `IRepoStorage` — storage abstraction (implemented by `SqlRepoTransactor`)
- `IBlobStore` — blob storage abstraction (implemented by `DiskBlobStore`/`S3BlobStore`)

### 6f. Identity (`src/projects/Identity/`)

- `IdResolver` — composite: `DidResolver` + `HandleResolver`
- `DidResolver` — dispatches to `PlcResolver` (did:plc) or `DidWebResolver` (did:web)
- `HandleResolver` — DNS TXT `_atproto.<handle>` + HTTPS `/.well-known/atproto-did`
- `MemoryCache` — stale-while-revalidate `ConcurrentDictionary` with TTL

### 6g. Crypto (`src/projects/Crypto/`)

- `Secp256k1Keypair` — `Create()`, `Import()`, `Sign()`, `Did()`, `Export()`
- `Verify` — `VerifySignature(didKey, data, sig)`
- `Did` — DID key parsing/formatting with plugin architecture (`IDidKeyPlugin`)
- `Secp256k1Wrapper` — thread-safe (locked) wrapper around native `Secp256k1Net`

### 6h. Host Services (`src/atompds/Services/`)

- `BackgroundJobQueue` + `BackgroundJobWorker` — bounded `Channel<Func<IServiceProvider, Task>>` (capacity 1000, `DropOldest`), consumed by a `BackgroundService` host
- `WriteSnapshotCache` — tracks recent repo writes by DID+collection (2-min TTL) for read-after-write consistency; `WriteSnapshot` data class holds Did, Collection, Rkey, RecordJson, Cid, Rev
- `OAuthSessionStore` — in-memory OAuth PKCE flow: `OAuthAuthorization` (10-min expiry, codeChallenge) → `OAuthCode` (1-min expiry, one-time use, S256 verification)
- `CaptchaVerifier` — validates hCaptcha tokens via `https://api.hcaptcha.com/siteverify` when secret configured
- `ServiceJwtBuilder` — creates signed service JWTs (iss/aud/lxm/iati/exp/jti claims) using actor signing key
- `IScratchCache` → `MemoryScratchCache` — `ConcurrentDictionary`-backed ephemeral cache with optional TTL

### 6i. Host Middleware (`src/atompds/Middleware/`)

- `AuthMiddleware` — inspects endpoint metadata attributes (`[AdminToken]`, `[AccessStandard]`, `[AccessFull]`, `[AccessPrivileged]`, `[Refresh]`) and invokes `AuthVerifier`
- `AuthVerifier` — Bearer/DPoP JWT validation, basic auth for admin, refresh token verification, scope checking, takedown/deactivation checks
- `RateLimitMiddleware` — three sliding-window policies: `per-ip-global` (500/min), `auth-sensitive` (30/min), `repo-write` (100/min); only active when `PDS_RATE_LIMITS_ENABLED=true`
- `NotFoundMiddleware` — logs warnings for 404 responses (filename has typo: `NotFoundMiddlware.cs`)

---

## 7. Configuration Records (`src/pds_projects/Config/`)

All config is immutable records mapped from `ServerEnvironment`:

| Record | Key Fields |
|--------|------------|
| `ServiceConfig` | Port, Hostname, PublicUrl, Did, Version, BlobUploadLimitInBytes, DevMode |
| `DatabaseConfig` | AccountDbLoc, SequencerDbLoc, DidCacheDbLoc, DisableWalAutoCheckpoint |
| `ActorStoreConfig` | Directory, CacheSize, DisableWalAutoCheckpoint |
| `BlobStoreConfig` | (base) → `DiskBlobstoreConfig` / `S3BlobstoreConfig` |
| `IdentityConfig` | PlcUrl, CacheStaleTTL, CacheMaxTTL, ResolverTimeout, ServiceHandleDomains, RecoveryDidKey, EnableDidDocWithSession |
| `InvitesConfig` | (abstract) → `RequiredInvitesConfig` / `NonRequiredInvitesConfig` |
| `SubscriptionConfig` | MaxSubscriptionBuffer, RepoBackfillLimitMs |
| `SecretsConfig` | JwtSecret, PlcRotationKey |
| `ProxyConfig` | DisableSsrfProtection, AllowHTTP2, HeadersTimeout, BodyTimeout, MaxResponseSize, MaxRetries, PreferCompressed |
| `IBskyAppViewConfig` | → `BskyAppViewConfig` / `DisabledBskyAppViewConfig` |

---

## 8. XRPC Error System (`src/pds_projects/Xrpc/`)

**`XRPCError`** — typed exception with `ResponseType` (HTTP status), `Error` (machine string), `Detail`.

**Error detail records** (all extend `ErrorDetail`):
`InvalidRequestErrorDetail` (400), `AuthRequiredErrorDetail` (401), `InvalidTokenErrorDetail`, `ExpiredTokenErrorDetail`, `InvalidInviteCodeErrorDetail`, `IncompatibleDidDocErrorDetail`, `InvalidHandleErrorDetail`, `UnsupportedDomainErrorDetail`, `InvalidPasswordErrorDetail`, `HandleNotAvailableErrorDetail`, `AccountTakenDownErrorDetail`

**`ResponseType` enum:** maps HTTP codes → named types (Unknown, InvalidRequest, AuthRequired, Forbidden, etc.)

---

## 9. Testing

**Framework:** TUnit (NOT xUnit). Uses `[Test]`, `[Arguments]`, `Assert.That(...).IsEqualTo(...)`.

| Project | What it tests |
|---------|---------------|
| `test/CID.Tests/` | CID v0/v1 creation, parsing, round-trip, multibase encoding, blob hashing (12 tests) |
| `test/Common.Tests/` | TID generation/parsing/ordering, S32 encoding, CBOR round-trip (10 tests) |
| `test/ActorStore.Tests/` | `Prepare.ExtractBlobReferences` — comprehensive validation of blob reference extraction from JSON (156 tests) |
| `test/SubscribeTester/` | NOT a test — manual WebSocket diagnostic tool |

**Test commands:**
```bash
dotnet test test/CID.Tests/CID.Tests.csproj
dotnet test test/Common.Tests/Common.Tests.csproj
dotnet test test/ActorStore.Tests/ActorStore.Tests.csproj
```

---

## 10. Key Patterns & Conventions

| Pattern | Where |
|---------|-------|
| **Unit-of-Work + Facade** | `ActorRepository` wraps `ActorStoreDb` + sub-repos with `TransactDbAsync`/`TransactRepoAsync` |
| **Factory** | `ActorRepositoryProvider`, `BlobStoreFactory` create per-actor instances |
| **Discriminated config records** | `BlobStoreConfig`, `InvitesConfig`, `IBskyAppViewConfig` use inheritance, consumed via `switch` pattern matching |
| **CBOR serialization** | All event types and commits implement `ICborEncodable<T>` with `ToCborObject()`/`static FromCborObject()` |
| **Two-phase blob lifecycle** | Upload to temp → promote to permanent when record references it → delete on deref |
| **Per-actor SQLite** | Each DID gets `store.sqlite` under SHA-256-hashed directory with its own migration history |
| **Background polling + channel fan-out** | `SequencerRepository` polls DB → `OnEvents` → `Outbox` streams via `Channel<ISeqEvt>` to WebSocket |
| **Plugin architecture** | `IDidKeyPlugin` for crypto key types (currently only secp256k1) |
| **CarpaNet source generation** | Lexicon JSONs in `CommonWeb/lexicons/` generate JSON/CBOR serializer contexts |
| **Central Package Management** | All NuGet versions in `Directory.Packages.props` |
| **Auth via middleware attributes** | `[AccessStandard]`, `[AccessPrivileged]`, etc. on controller actions, processed by `AuthMiddleware` |
| **Background job queue** | `BackgroundJobQueue` (bounded `Channel`, capacity 1000, `DropOldest`) → `BackgroundJobWorker` (`BackgroundService`) |
| **Rate limiting** | Three sliding-window policies: `per-ip-global` (500/min), `auth-sensitive` (30/min), `repo-write` (100/min) — disabled by default |
| **OAuth PKCE flow** | `OAuthSessionStore` manages in-memory authorizations + code exchange with S256 challenge |
| **Write snapshot cache** | `WriteSnapshotCache` tracks recent writes by DID+collection with 2-min TTL for read-after-write consistency |
| **Scratch cache** | `IScratchCache` → `MemoryScratchCache` — `ConcurrentDictionary` with optional TTL |
| **Service JWT** | `ServiceJwtBuilder` creates signed JWTs for inter-service auth with iss/aud/lxm claims |
| **hCaptcha verification** | `CaptchaVerifier` validates tokens via hCaptcha API when secret is configured |

---

## 11. Hot Files (read before changing related code)

| File | Why |
|------|-----|
| `src/atompds/Program.cs` | Startup pipeline, middleware order, static endpoints |
| `src/atompds/Config/ServerConfig.cs` | All DI registration, config mapping |
| `src/atompds/Config/ServerEnvironment.cs` | All env variable bindings (64 properties) |
| `src/atompds/Middleware/AuthMiddleware.cs` | Auth attribute definitions |
| `src/atompds/Middleware/AuthVerifier.cs` | JWT validation, DPoP, token verification |
| `src/atompds/Middleware/RateLimitMiddleware.cs` | Rate limiting policies (per-ip, auth-sensitive, repo-write) |
| `src/atompds/Services/BackgroundJobQueue.cs` | Background job queue + worker |
| `src/atompds/Services/WriteSnapshotCache.cs` | Read-after-write consistency |
| `src/atompds/Services/OAuth/OAuthSessionStore.cs` | OAuth PKCE flow state |
| `src/atompds/Services/CaptchaVerifier.cs` | hCaptcha token verification |
| `src/atompds/Services/ServiceJwtBuilder.cs` | Inter-service JWT creation |
| `src/atompds/Controllers/OAuth/` | OAuth authorize/token flow |
| `src/atompds/Controllers/WellKnownController.cs` | .well-known endpoints |
| `src/atompds/Controllers/Xrpc/AppViewProxyController.cs` | AppView proxy (30+ endpoints) |
| `src/pds_projects/AccountManager/AccountRepository.cs` | Account creation, login, session management |
| `src/pds_projects/AccountManager/Auth.cs` | JWT creation, refresh token rotation |
| `src/pds_projects/ActorStore/ActorRepositoryProvider.cs` | Per-actor store lifecycle |
| `src/pds_projects/ActorStore/Repo/Prepare.cs` | Write preparation, blob extraction, slur checking |
| `src/pds_projects/Sequencer/Outbox.cs` | Concurrency-sensitive backfill/cutover/streaming |
| `src/pds_projects/Sequencer/SequencerRepository.cs` | Event sequencing, background polling |
| `src/pds_projects/AccountManager/Db/AccountStore.cs` | Account deactivation date parsing |
| `src/projects/Repo/Repo.cs` | Commit creation, write application |
| `src/projects/Repo/MST/MST.cs` | Merkle Search Tree core |
| `src/projects/Identity/BaseResolver.cs` | DID resolution with caching |
| `src/projects/Crypto/Secp256k1/Secp256k1Keypair.cs` | Key management |
| `src/projects/CommonWeb/Util.cs` | DID/handle/AT-URI validation |

---

## 12. Admin CLI (`src/pdsadmin/`)

Single-file app using `ConsoleAppFramework`. Two command classes:

**`AccountCommands`** (subcommand `"account"`): `list`, `create`, `admin-delete`, `info`, `update-handle`, `update-email`, `enable-invites`, `disable-invites`, `delete`, `takedown`, `untakedown`, `reset-password`.

**`RootCommands`:** `request-crawl`, `create-invite-code`, `update` (not implemented).

Config from `pdsenv.json` (`PdsHostname`, `PdsAdminPassword`).

---

## 13. Migration Tool (`src/migration/`)

Uses DurableTask to batch-migrate all per-actor SQLite databases. Discovers actor DBs under `PDS_DATA_DIRECTORY/actors/`, runs EF migrations sequentially, rolls back on failure.

---

## 14. NuGet Packages Worth Knowing

| Package | Purpose |
|---------|---------|
| `PeterO.Cbor` | CBOR encoding/decoding (used throughout for repo blocks and events) |
| `jose-jwt` | JWT creation and verification |
| `Microsoft.IdentityModel.JsonWebTokens` | JWT handling for DPoP/auth |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT bearer auth middleware |
| `CarpaNet` | Source generator for ATProto lexicon types (JSON + CBOR contexts) |
| `Secp256k1.Net` | Native secp256k1 crypto |
| `SimpleBase` | Base32/58/64 encoding |
| `Multiformats.*` | CID multibase/codec/hash |
| `Scrypt.NET` | Password hashing |
| `DnsClient` | DNS TXT record lookup for handle resolution |
| `Ipfs.Core` | Varint encoding for CAR files |
| `ConsoleAppFramework` | CLI framework for pdsadmin |
| `TUnit` | Test framework |
| `MailKit` | SMTP email sending |
| `Newtonsoft.Json` | Legacy JSON handling |
| `Scalar.AspNetCore` | OpenAPI/Scalar API documentation UI |
| `System.Drawing.Common` | Image dimension detection for blobs |
| `AWSSDK.S3` | S3 blob storage backend |
| `Microsoft.EntityFrameworkCore.Sqlite` | SQLite ORM for all databases |
| `Microsoft.Azure.DurableTask.Core` | Durable task framework for migration tool |
