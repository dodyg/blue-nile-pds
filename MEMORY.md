# MEMORY.md -- Codebase Reference for Agents

This document is a structured map of the `blue-nile-pds` codebase. Read this first to orient yourself before making changes.

---

## 1. Project Overview

`blue-nile-pds` is a .NET 10 preview C# implementation of an ATProto Personal Data Server (PDS). It is a fork of `atompds`, experimental and learning-focused. It hosts user accounts, stores repos (Merkle Search Trees), serves blobs, sequences events to a firehose, and exposes 65 `com.atproto.*` XRPC endpoints plus 36 AppView proxy route registrations, OAuth (4 endpoints), health, 3 well-known endpoints, and an error handler. Total: ~111 HTTP route registrations implemented as ASP.NET Core Minimal API endpoints across ~65 endpoint files under `src/atompds/Endpoints/`.

- **SDK:** .NET 10 (`10.0.100-rc.1`, pinned in `global.json`, `rollForward: latestMinor`, test runner: `Microsoft.Testing.Platform`)
- **Test framework:** TUnit (NOT xUnit despite what AGENTS.md says)
- **Solution file:** `atompds.slnx` (XML-based slnx format)
- **Central package management:** `Directory.Packages.props` (CPM)
- **Build props:** `Directory.Build.props` (applies `Microsoft.VisualStudio.Threading.Analyzers` to all projects)
- **Build note:** Default `dotnet build` may hit MSB4166 node crashes on resource-constrained machines. Use `-m:1` or `-m:2` if this occurs.

---

## 2. Solution Structure

```
atompds.slnx
├── src/atompds/              ASP.NET Core host (web app entry point, Minimal API endpoints, middleware, services)
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
    ├── CID.Tests/             CID parsing, creation, round-trip, multibase, blob hashing (17 tests)
    ├── Common.Tests/          TID, S32 encoding, CBOR round-trip (12 tests)
    ├── Crypto.Tests/          Secp256k1 keypair lifecycle, signing, verification, DID key parsing (26 tests)
    ├── Repo.Tests/            MST insert/delete/walk, CAR encoding, round-trip (11 tests)
    ├── ActorStore.Tests/      `Prepare.ExtractBlobReferences` — blob reference extraction from JSON (156 tests)
    ├── atompds.Tests/         Integration tests — all XRPC namespaces via WebApplicationFactory (282 tests; 176 pass, 101 fail, 5 skip — pre-existing failures in account/session/crud flows)
    ├── SubscribeTester/       Manual WebSocket subscription test tool
    └── data/                  Shared test data (blob files)
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
  └─ Repo                    ├─ Repo ────── Crypto, CID, Common, CommonWeb
       └─ MST, CAR, commits  │
                              ├─ CommonWeb ── Xrpc (pds_projects)
                              │    └─ DidDocument, Util, CarpaNet types
                              │
                              └─ Handle ──── Identity, Config, Xrpc

pds_projects layer (Config depends on Crypto):
  Config ←─ AccountManager ←── Sequencer
          ←─ ActorStore   ←──┘
          ←─ BlobStore ←─ Repo
          ←─ Mailer
          ←─ Xrpc (error types used by nearly everything)

  AccountManager → CID, CommonDb, Config, Xrpc
  ActorStore → BlobStore, CID, CommonDb, DidLib, Handle, Repo, Config, Xrpc
  Sequencer → AccountManager, ActorStore

atompds host references ALL pds_projects + Repo
```

---

## 4. Key Entry Points

### Web Host: `src/atompds/Program.cs`

1. Creates `WebApplication.CreateSlimBuilder(args)` with `AddCors()` + `AddHttpClient()`
2. Reads `Config` section from appsettings → `ServerEnvironment`
3. Creates `ServerConfig` (validates env, expands paths, maps sub-configs)
4. `ServerConfig.RegisterServices()` wires all DI (see DI section below)
5. Configures JSON serialization via `ConfigureHttpJsonOptions` (ignore defaults), HTTP logging as a service (middleware call commented out), XRPC exception handler
6. Enables rate limiting (`AddPdsRateLimiting`) when `PDS_RATE_LIMITS_ENABLED` is true
7. Registers `BackgroundJobQueue` (singleton), `IBackgroundJobQueue` (singleton), `ChannelWriter<Func<IServiceProvider, Task>>` (singleton), `BackgroundEmailDispatcher` (singleton), `BackgroundJobWorker` (hosted service) — all in Program.cs, NOT in `RegisterServices()`
8. Auto-migrates `AccountManagerDb` and `SequencerDb` on startup
9. Middleware pipeline: routing → rate limiter (conditional) → `MapEndpoints(...)` → exception handler (`/error`) → auth middleware → not-found middleware → WebSockets → CORS
10. Root/static endpoints (`/`, `/robots.txt`, `/tls-check`) are registered in `RootEndpoints.cs` via `MapRootEndpoints()`

Note: `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore` packages are referenced in the .csproj but not configured or invoked in Program.cs.

### Config: `src/atompds/Config/ServerEnvironment.cs`

All config is bound from `appsettings.Development.json` `Config` section. Contains **71 properties** (70 `PDS_*` + `InviteEpoch`). Key required fields: `PDS_JWT_SECRET`, `PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX`, `PDS_BLOBSTORE_DISK_LOCATION`, `PDS_BLOBSTORE_DISK_TMP_LOCATION`. See `appsettings.Development.json.example`.

Property groups: Service Configuration (7), Data Directories (4), Actor Store (2), Blobstore Disk (2), Blobstore S3 (7), Identity (7), Invites (3), Subscription (2), Bsky AppView (3), Crawlers (1), Secrets (2), Fetch (2), Proxy (6), Server Metadata/Branding (8 — `PDS_SERVICE_NAME`, `PDS_PRIVACY_POLICY_URL`, `PDS_TERMS_OF_SERVICE_URL`, `PDS_HOME_URL`, `PDS_SUPPORT_URL`, `PDS_LOGO_URL`, `PDS_CONTACT_EMAIL`, `PDS_PHONE_VERIFICATION_REQUIRED`), SMTP (6), Rate Limiting (1), Anti-Abuse/hCaptcha (2), OAuth Entryway (4 — `PDS_OAUTH_ENTRYWAY_URL`, `PDS_OAUTH_ENTRYWAY_DID`, `PDS_OAUTH_ENTRYWAY_JWT_VERIFY_KEY_K256_PUBLIC_KEY_HEX`, `PDS_OAUTH_TRUSTED_CLIENTS`), Moderation (2 — `PDS_REPORT_SERVICE_URL`, `PDS_REPORT_SERVICE_DID`), Redis (1 — `PDS_REDIS_URL`).

### DI Wiring: `src/atompds/Config/ServerConfig.cs`

`RegisterServices()` registers:
- All config sub-records as singletons
- `AccountManagerDb` (scoped DbContext, SQLite)
- `SequencerDb` (DbContextFactory, SQLite)
- `AccountRepository`, `AccountStore`, `PasswordStore`, `RepoStore`, `InviteStore`, `EmailTokenStore`, `AppPasswordStore`, `Auth` (all scoped)
- `ActorRepositoryProvider` (scoped, creates per-actor repos)
- `BlobStoreFactory` (singleton)
- `IdResolver`, `HandleManager`, `IDidCache` → `MemoryCache` (singleton)
- `IdentityResolverOpts` (singleton)
- `AuthVerifier`, `ServiceJwtBuilder` (scoped)
- `AuthVerifierConfig` (singleton)
- `SequencerRepository` (scoped), `Crawlers` (singleton), `CrawlersConfig` (singleton)
- `PlcClient`, `PlcClientConfig` (singleton)
- `IMailer` → `SmtpMailer` or `StubMailer` based on SMTP config (singleton)
- `CaptchaVerifier` (singleton, for hCaptcha token verification)
- `EmailAddressValidator` (singleton, validates emails + checks disposable domains via Kickbox API)
- `WriteSnapshotCache` (singleton, read-after-write consistency for records)
- `OAuthSessionStore` (singleton, in-memory OAuth PKCE flow state)
- `IScratchCache` → `MemoryScratchCache` or `RedisScratchCache` based on `PDS_REDIS_URL` (singleton)
- `IConnectionMultiplexer` (singleton, StackExchange.Redis, only when `PDS_REDIS_URL` is set)
- `ReservedSigningKeyStore` (singleton, reserves signing keys in scratch cache with 1hr TTL)
- `EntrywayRelayService` (scoped, forwards requests to OAuth entryway)

Registered in `Program.cs` (NOT in `RegisterServices()`):
- `BackgroundJobQueue` (singleton) + `IBackgroundJobQueue` (singleton) + `ChannelWriter<Func<IServiceProvider, Task>>` (singleton)
- `BackgroundEmailDispatcher` (singleton, enqueues email-sending jobs onto background queue)
- `BackgroundJobWorker` (hosted service, bounded channel)

---

## 5. Endpoint Architecture

### Location & Naming

All HTTP endpoints use ASP.NET Core Minimal APIs. No MVC controllers exist. Endpoint files live under `src/atompds/Endpoints/` organized by ATProto namespace. `EndpointRegistration.cs` contains the `MapEndpoints()` extension method that wires all routes into the app.

| Namespace | Directory | Endpoints | Notes |
|-----------|-----------|-----------|-------|
| `com.atproto.admin.*` | `Endpoints/Xrpc/Com/Atproto/Admin/` | 13 | 11 files; `AccountInvitesAdminEndpoints` has enable+disable (2), `SubjectStatusEndpoints` has get+update (2) |
| `com.atproto.identity.*` | `Endpoints/Xrpc/Com/Atproto/Identity/` | 6 | |
| `com.atproto.repo.*` | `Endpoints/Xrpc/Com/Atproto/Repo/` | 8 | `ApplyWritesEndpoints` has 5 endpoints (getRecord, putRecord, deleteRecord, createRecord, applyWrites) |
| `com.atproto.server.*` | `Endpoints/Xrpc/Com/Atproto/Server/` | 25 | `DeleteAccountEndpoints` has requestAccountDelete + deleteAccount (2) |
| `com.atproto.sync.*` | `Endpoints/Xrpc/Com/Atproto/Sync/` | 11 | Includes `listMissingBlobs`, `importRepo` |
| `com.atproto.moderation.*` | `Endpoints/Xrpc/Com/Atproto/Moderation/` | 1 | `createReport` |
| `com.atproto.temp.*` | `Endpoints/Xrpc/Com/Atproto/Temp/` | 1 | `checkSignupQueue` |
| `app.bsky.*` + `chat.bsky.*` | `Endpoints/Xrpc/AppViewProxyEndpoints.cs` | 36 route registrations | Catchall `{nsid}` GET+POST; **must be registered last** so specific routes take priority |
| (health) | `Endpoints/Xrpc/HealthEndpoints.cs` | 1 | `_health` |
| (OAuth) | `Endpoints/OAuth/` | 4 | 3 files: `authorize` (GET), `authorize/consent` (POST), `token` (POST), `client-metadata.json` (GET) |
| (well-known) | `Endpoints/WellKnownEndpoints.cs` | 3 | `oauth-protected-resource`, `oauth-authorization-server`, `atproto-did` |
| (error) | `Endpoints/ErrorEndpoints.cs` | 1 method (GET+POST) | Exception handler target for `/error` |
| (root) | `Endpoints/RootEndpoints.cs` | 3 | `/` (server info JSON), `/robots.txt`, `/tls-check` |

### Endpoint Pattern

```csharp
namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class XxxEndpoints
{
    public static RouteGroupBuilder MapXxxEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.namespace.method", Handle)
            .WithMetadata(new AccessStandardAttribute()); // auth
        // .RequireRateLimiting("auth-sensitive") for rate-limited endpoints
        return group;
    }

    public static async Task<IResult> Handle(
        HttpContext context,
        SomeService someService)   // DI via method parameters
    {
        var auth = context.GetAuthOutput(); // extract auth when needed
        // validate input, throw XRPCError on failure
        // call services
        return Results.Ok(new { ... });
    }
}
```

### Streaming & Upload Patterns

- **CAR/blob streaming** (`GetRepo`, `GetBlocks`, `GetBlob`, `GetRecord`): return `Results.Stream(stream, contentType)` instead of writing to `Response.Body`
- **File upload** (`BlobEndpoints`, `ImportRepoEndpoints`): accept `HttpRequest request` parameter and read `request.Body`
- **WebSocket** (`SubscribeReposEndpoints`): accept `HttpContext context`, call `context.WebSockets.AcceptWebSocketAsync()`; registered as `MapGet`

### Auth Attributes (defined in `AuthMiddleware.cs`)

| Attribute | Meaning | Applied via |
|-----------|---------|-------------|
| `[AdminToken]` | Basic auth admin:password | `.WithMetadata(new AdminTokenAttribute())` |
| `[AccessStandard]` | Accepts access + appPass + appPassPrivileged JWTs | `.WithMetadata(new AccessStandardAttribute())` |
| `[AccessFull]` | Accepts only access JWT (no app passwords) | `.WithMetadata(new AccessFullAttribute())` |
| `[AccessPrivileged]` | Accepts access + appPassPrivileged JWTs | `.WithMetadata(new AccessPrivilegedAttribute())` |
| `[Refresh]` | Requires refresh JWT | `.WithMetadata(new RefreshAttribute())` |

Attributes take optional `(bool checkTakenDown, bool checkDeactivated)` params.

### Error Handling

- Throw `XRPCError(new XxxErrorDetail("message"))` for API errors
- Error detail types: `InvalidRequestErrorDetail`, `AuthRequiredErrorDetail`, `InvalidTokenErrorDetail`, `ExpiredTokenErrorDetail`, `InvalidInviteCodeErrorDetail`, `HandleNotAvailableErrorDetail`, `AccountTakenDownErrorDetail`, etc.
- `XRPCExceptionHandler` converts `XRPCError` to HTTP responses
- All error types defined in `src/pds_projects/Xrpc/Errors.cs`

---

## 6. Core Subsystems

### 6a. Actor Store (`src/pds_projects/ActorStore/`)

Each user gets their own SQLite database at `<actor_dir>/<sha256(did)>/<did_colons_as_underscores>/store.sqlite`.

**Key classes:**
- `ActorRepositoryProvider` — factory: `GetLocation(did)`, `Open(did)`, `Create(did, keyPair)`, `Destroy(did)`, `Exists(did)`, `KeyPair(did)`
- `ActorRepository` — unit-of-work facade wrapping `ActorStoreDb` + sub-repositories: `Repo`, `Record`, blobs. Methods: `TransactDbAsync`, `TransactRepoAsync`, `ListCollections`
- `ActorStoreDb` — `DbContext` with 7 `DbSet<>` properties, full `OnModelCreating` configuration
- `SqlRepoTransactor` — implements `IRepoStorage`, bridges MST layer to SQLite blocks table. Methods: `GetRootAsync`, `PutBlockAsync`, `PutManyAsync`, `UpdateRootAsync`, `ApplyCommitAsync`, `GetBytesAsync`, `HasAsync`, `GetBlocksAsync`, `ReadObjAndBytesAsync`, `AttemptReadAsync`, `GetRootDetailedAsync`, `CacheRevAsync`, `DeleteManyAsync`, `IterateCarBlocksAsync`, `GetBlockRangeAsync`
- `RepoRepository` — `CreateRepoAsync`, `ProcessWritesAsync`, `FormatCommitAsync`, `GetCollectionsAsync`, `IndexWritesAsync`
- `RecordRepository` — `GetRecordAsync`, `IndexRecordAsync`, `DeleteRecordAsync`, `ListRecordsForCollectionAsync`, `RemoveBacklinksByUriAsync`, `AddBacklinksAsync`, `GetBacklinks`
- `BlobTransactor` — blob lifecycle (temp→permanent, deref cleanup). Methods: `GetBlobAsync`, `GetRecordsForBlobAsync`, `ListBlobsAsync`, `GenerateTempBlobMetadataAsync`, `SaveBlobRecordAsync`, `UpdateBlobAsync`, `ProcessWriteBlobsAsync`
- `Prepare` — static helpers: `PrepareCreate`, `PrepareUpdate`, `PrepareDelete`, `ExtractBlobReferences` (string + JsonElement overloads), `TryExtractBlobReference`, `CidForSafeRecord`, `AssertNoExplicitSlurs`

**DB tables (per actor):** `repo_root`, `repo_block`, `record`, `backlink`, `blob`, `record_blob`, `account_pref`

**Enums:** `BlobStatus` (`Temporary`, `Permanent`, `GarbageCollected`)

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
- `SequencerRepository` — `SequenceCommitAsync`, `SequenceHandleUpdateAsync`, `SequenceIdentityEventAsync`, `SequenceAccountEventAsync`, `SequenceTombstoneEventAsync`, `SequenceEventAsync`, `GetRangeAsync`, `CurrentAsync`, `NextAsync`, `EarliestAfterTimeAsync`, `DeleteAllForUserAsync`. Background polling with `OnEvents` event and `OnClose` event. Constructor takes `ChannelWriter<Func<IServiceProvider, Task>>` for background job integration.
- `Outbox` — bounded `Channel<ISeqEvt>` fan-out to WebSocket consumers. Supports backfill + live cutover.
- `Crawlers` — rate-limited notification to relay/BGS hosts (POST `requestCrawl`, 20-min notify threshold)

**Event types:** `CommitEvt`, `HandleEvt`, `IdentityEvt`, `AccountEvt`, `TombstoneEvt` (all `ICborEncodable<T>`, stored as CBOR `byte[]`). Typed wrappers `TypedCommitEvt`, `TypedHandleEvt`, `TypedIdentityEvt`, `TypedAccountEvt`, `TypedTombstoneEvt` implement `ISeqEvt` (adds `Type`, `Seq`, `Time`). `CommitEvtOp` holds per-operation action/path/cid. `CommitEvtAction` enum: `Create`, `Update`, `Delete`.

**Enums:** `RepoSeqEventType` (`Append`, `Rebase`, `Handle`, `Migrate`, `Identity`, `Account`, `Tombstone`), `TypedCommitType` (`Commit`, `Handle`, `Identity`, `Account`, `Tombstone`)

**DB table:** `RepoSeqs` (Seq auto-inc PK, Did, EventType, Event bytes, Invalidated, SequencedAt)

### 6d. Blob Store (`src/pds_projects/BlobStore/`)

- `IBlobStore` — blob storage abstraction (defined in `src/projects/Repo/IRepoStorage.cs`)
- `BlobStoreFactory` — creates `DiskBlobStore` or `S3BlobStore` per config
- `DiskBlobStore` — file system: `<location>/<did>/<cid>` permanent, `<tmp>/<did>/<key>` temp
- `S3BlobStore` — AWS S3: `blocks/<did>/<cid>` permanent, `tmp/<did>/<key>` temp. Also defines `BlobNotFoundException`

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
- `Verify` — `VerifySignature(didKey, data, sig, opts, jwtAlg)` — dispatches to `IDidKeyPlugin.VerifySignature`
- `Did` — DID key parsing/formatting with plugin architecture (`IDidKeyPlugin`)
- `Operations` — `VerifySig`, `VerifyDidSig`, `IsCompactFormat` — note: `VerifySig` passes compact-format signatures directly to `Secp256k1Net.Secp256k1.Verify` (static method expects compact, not internal format)
- `Secp256k1Wrapper` — thread-safe (locked) wrapper around native `Secp256k1Net`

### 6h. Host Services (`src/atompds/Services/`)

- `BackgroundJobQueue` + `BackgroundJobWorker` — bounded `Channel<Func<IServiceProvider, Task>>` (capacity 1000, `DropOldest`), `IBackgroundJobQueue` interface, consumed by a `BackgroundService` host
- `BackgroundEmailDispatcher` — enqueues email-sending jobs (`SendCustomEmail`, `SendAccountDelete`, `SendEmailConfirmation`, `SendEmailUpdate`, `SendPasswordReset`, `SendPlcOperationSignature`) onto `IBackgroundJobQueue`
- `WriteSnapshotCache` — tracks recent repo writes by DID+collection (2-min TTL) for read-after-write consistency; `WriteSnapshot` data class holds Did, Collection, Rkey, RecordJson, Cid, Rev
- `OAuthSessionStore` — in-memory OAuth PKCE flow: `OAuthAuthorization` (10-min expiry, codeChallenge) → `OAuthCode` (1-min expiry, one-time use, S256 verification)
- `CaptchaVerifier` — validates hCaptcha tokens via `https://api.hcaptcha.com/siteverify` when secret configured
- `EmailAddressValidator` — validates email structure + checks disposable email domains via Kickbox API (`https://open.kickbox.com/v1/disposable/{email}`)
- `ServiceJwtBuilder` — creates signed service JWTs (iss/aud/lxm/iati/exp/jti claims) using actor signing key
- `IScratchCache` → `MemoryScratchCache` — `ConcurrentDictionary`-backed ephemeral cache with optional TTL
- `IScratchCache` → `RedisScratchCache` — Redis-backed ephemeral cache (via StackExchange.Redis `IConnectionMultiplexer`), used when `PDS_REDIS_URL` is configured
- `ReservedSigningKeyStore` — generates and reserves secp256k1 signing keypairs in scratch cache (1hr TTL); `ReserveAsync(did)` creates, `ConsumeAsync(did)` retrieves and deletes
- `EntrywayRelayService` — HTTP reverse-proxy to OAuth entryway server; `ForwardJsonAsync`, `ForwardFormAsync`, `ForwardWithoutBodyAsync`; attaches service JWT Bearer header

### 6i. Other Host Code

- `ExceptionHandler/XRPCExceptionHandler.cs` — implements `IExceptionHandler`; handles three exception types: `XRPCError` → XRPC-formatted JSON `{ error, message }` with appropriate HTTP status; `JsonException` → 400 `InvalidRequest`; `BadHttpRequestException` → 400 `InvalidRequest`
- `StaticConfig.cs` — constants: `DbVersion` ("1.0.0"), `Version` (assembly-derived version string)
- `Utils/Extensions.cs` — C# 13 extension adding `ToJsonElement()` to `DidDocument`
- `Utils/CursorUtils.cs` — pagination cursor packing/unpacking (two-part `::`-separated strings)

### 6j. Host Middleware (`src/atompds/Middleware/`)

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
| `BlobStoreConfig` | (base) → `DiskBlobstoreConfig` (Provider, Location, TempLocation) / `S3BlobstoreConfig` (Provider, Bucket, Region, Endpoint, ForcePathStyle, AccessKeyId, SecretAccessKey, UploadTimeoutMs) |
| `IdentityConfig` | PlcUrl, CacheStaleTTL, CacheMaxTTL, ResolverTimeout, ServiceHandleDomains, RecoveryDidKey, EnableDidDocWithSession |
| `InvitesConfig` | (abstract) → `RequiredInvitesConfig` (Interval, Epoch) / `NonRequiredInvitesConfig` |
| `SubscriptionConfig` | MaxSubscriptionBuffer, RepoBackfillLimitMs |
| `SecretsConfig` | JwtSecret, PlcRotationKey (`Secp256k1Keypair` type, not string) |
| `ProxyConfig` | DisableSsrfProtection, AllowHTTP2, HeadersTimeout, BodyTimeout, MaxResponseSize, MaxRetries, PreferCompressed |
| `IBskyAppViewConfig` | → `BskyAppViewConfig` (Url, Did, CdnUrlPattern) / `DisabledBskyAppViewConfig` |

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
| `test/CID.Tests/` | CID parsing, creation, round-trip, multibase encoding, blob hashing (17 tests) |
| `test/Common.Tests/` | TID generation/parsing/ordering, S32 encoding, CBOR round-trip (12 tests) |
| `test/Crypto.Tests/` | Secp256k1 keypair creation/import/export, signing, signature verification, DID key parsing/formatting, round-trip (26 tests) |
| `test/Repo.Tests/` | MST insert/delete/walk, node splitting, CAR block encoding, data diff, round-trip (11 tests) |
| `test/ActorStore.Tests/` | `Prepare.ExtractBlobReferences` — comprehensive validation of blob reference extraction from JSON (156 tests) |
| `test/atompds.Tests/` | Integration tests via `WebApplicationFactory<Program>` (282 tests): auth gatekeeping, route existence, CRUD flows, account lifecycle, sequencer events. **101 tests fail** (pre-existing) — primarily account creation/session flows returning 400 errors. Uses `TestWebAppFactory` + `AuthTestHelper` + `AccountHelper` in `Infrastructure/` |
| `test/SubscribeTester/` | NOT a test — manual WebSocket diagnostic tool |

**Test commands:**
```bash
dotnet test test/CID.Tests/CID.Tests.csproj
dotnet test test/Common.Tests/Common.Tests.csproj
dotnet test test/Crypto.Tests/Crypto.Tests.csproj
dotnet test test/Repo.Tests/Repo.Tests.csproj
dotnet test test/ActorStore.Tests/ActorStore.Tests.csproj
dotnet test test/atompds.Tests/atompds.Tests.csproj
```

**Total: 504 tests across 6 test projects** (398 pass, 101 fail pre-existing in atompds.Tests, 5 skipped).

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
| **Auth via endpoint metadata** | `[AccessStandard]`, `[AccessPrivileged]`, etc. applied to Minimal API routes via `.WithMetadata(new AccessStandardAttribute())`, processed by `AuthMiddleware` |
| **Background job queue** | `BackgroundJobQueue` (bounded `Channel`, capacity 1000, `DropOldest`) → `BackgroundJobWorker` (`BackgroundService`) |
| **Rate limiting** | Three sliding-window policies: `per-ip-global` (500/min), `auth-sensitive` (30/min), `repo-write` (100/min) — disabled by default |
| **OAuth PKCE flow** | `OAuthSessionStore` manages in-memory authorizations + code exchange with S256 challenge |
| **Write snapshot cache** | `WriteSnapshotCache` tracks recent writes by DID+collection with 2-min TTL for read-after-write consistency |
| **Scratch cache** | `IScratchCache` → `MemoryScratchCache` (default) or `RedisScratchCache` (when `PDS_REDIS_URL` set) — ephemeral key-value cache with optional TTL |
| **Service JWT** | `ServiceJwtBuilder` creates signed JWTs for inter-service auth with iss/aud/lxm claims |
| **hCaptcha verification** | `CaptchaVerifier` validates tokens via hCaptcha API when secret is configured |
| **Email validation** | `EmailAddressValidator` checks structure + disposable domain via Kickbox API |
| **Reserved signing keys** | `ReservedSigningKeyStore` creates temporary keypairs, stores in scratch cache (1hr TTL), consumed on account creation |
| **Entryway relay** | `EntrywayRelayService` forwards OAuth requests to configured entryway with service JWT auth |
| **Background email dispatch** | `BackgroundEmailDispatcher` enqueues email-sending onto background job queue |
| **Pagination cursors** | `CursorUtils` packs/unpacks two-part `::`-separated cursor strings |

---

## 11. Hot Files (read before changing related code)

| File | Why |
|------|-----|
| `src/atompds/Program.cs` | Startup pipeline, middleware order, static endpoints |
| `src/atompds/Config/ServerConfig.cs` | All DI registration, config mapping |
| `src/atompds/Config/ServerEnvironment.cs` | All env variable bindings (71 properties) |
| `src/atompds/Middleware/AuthMiddleware.cs` | Auth attribute definitions |
| `src/atompds/Middleware/AuthVerifier.cs` | JWT validation, DPoP, token verification |
| `src/atompds/Middleware/RateLimitMiddleware.cs` | Rate limiting policies (per-ip, auth-sensitive, repo-write) |
| `src/atompds/Services/BackgroundJobQueue.cs` | Background job queue + worker + `IBackgroundJobQueue` interface |
| `src/atompds/Services/WriteSnapshotCache.cs` | Read-after-write consistency |
| `src/atompds/Services/OAuth/OAuthSessionStore.cs` | OAuth PKCE flow state |
| `src/atompds/Services/CaptchaVerifier.cs` | hCaptcha token verification |
| `src/atompds/Services/ServiceJwtBuilder.cs` | Inter-service JWT creation |
| `src/atompds/Services/BackgroundEmailDispatcher.cs` | Async email dispatch via background queue |
| `src/atompds/Services/EmailAddressValidator.cs` | Email structure + disposable domain validation |
| `src/atompds/Services/RedisScratchCache.cs` | Redis-backed scratch cache implementation |
| `src/atompds/Services/ReservedSigningKeyStore.cs` | Temporary signing key reservation |
| `src/atompds/Services/EntrywayRelayService.cs` | OAuth entryway request forwarding |
| `src/atompds/ExceptionHandler/XRPCExceptionHandler.cs` | `IExceptionHandler` for `XRPCError` → JSON responses |
| `src/atompds/Utils/CursorUtils.cs` | Pagination cursor packing/unpacking |
| `src/atompds/Endpoints/EndpointRegistration.cs` | Wires all route registrations; controls endpoint order (AppViewProxy last) |
| `src/atompds/Endpoints/RootEndpoints.cs` | `/`, `/robots.txt`, `/tls-check` — server info JSON |
| `src/atompds/Endpoints/OAuth/` | OAuth authorize/token flow |
| `src/atompds/Endpoints/WellKnownEndpoints.cs` | `.well-known/` endpoints |
| `src/atompds/Endpoints/Xrpc/AppViewProxyEndpoints.cs` | AppView proxy (catchall `{nsid}` GET+POST, registered last) |
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
| `src/projects/Crypto/Secp256k1/Operations.cs` | Signature verification — passes compact format directly to `Secp256k1Net` |
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
| `PeterO.Cbor` | CBOR encoding/decoding — version-pinned in CPM but not referenced by any .csproj |
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
| `Newtonsoft.Json` | Legacy JSON handling — version-pinned in CPM but not referenced by any .csproj |
| `Scalar.AspNetCore` | OpenAPI/Scalar API documentation UI — referenced but not configured in Program.cs |
| `System.Drawing.Common` | Image dimension detection — version-pinned in CPM but not referenced by any .csproj |
| `AWSSDK.S3` | S3 blob storage backend |
| `Microsoft.EntityFrameworkCore.Sqlite` | SQLite ORM for all databases |
| `Microsoft.EntityFrameworkCore.Design` | EF Core design-time tooling for migrations |
| `Microsoft.Azure.DurableTask.Core` | Durable task framework for migration tool |
| `Microsoft.DurableTask.SqlServer` | SQL Server backend for DurableTask (migration tool) |
| `StackExchange.Redis` | Redis client for `RedisScratchCache` |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI support — referenced but not configured in Program.cs |
| `Microsoft.AspNetCore.Mvc.Testing` | Integration test host (`WebApplicationFactory`) for atompds.Tests |
| `Microsoft.VisualStudio.Threading.Analyzers` | Roslyn analyzer (applied via `Directory.Build.props`) |
| `Microsoft.Extensions.Logging.*` | Logging abstractions, console, debug providers |
