# GAPS-TODO.md — Execution Plan

Work items to close the gaps in `GAPS.md`. Ordered by dependency and risk — implement phases in order, items within a phase can proceed in parallel.

**Goal:** Achieve functional equivalence with the canonical TypeScript PDS at `bluesky-social/atproto/packages/pds`.

---

## Phase 1 — Security & Correctness (Do First)

These are blocking correctness/security issues. Ship nothing to production until these are done.

---

### T-01: Typed Repository Error Hierarchy
**Gap:** 12.10  
**Priority:** Must-have (prerequisite for T-02, T-03)  
**File:** `src/projects/Repo/Errors.cs` (new)

Create typed exceptions matching the canonical `packages/repo/src/error.ts`:

```csharp
public class RepoException : Exception { ... }
public class MissingBlockException(Cid cid, string source) : RepoException { ... }
public class MissingBlocksException(Cid[] cids, string source) : RepoException { ... }
public class MissingCommitBlocksException(Cid commitCid, Cid[] missing) : RepoException { ... }
public class UnexpectedObjectException(Cid cid, string expectedType, string actualType) : RepoException { ... }
```

Replace generic `throw new Exception(...)` in `MST.cs`, `Repo.cs`, `SqlRepoTransactor.cs`, and `Provider.cs` with the appropriate typed exception. This is a prerequisite for T-02.

---

### T-02: Graceful MST Walk (`walkReachable` / `reachableLeaves`)
**Gap:** 12.5  
**Priority:** Must-have (prerequisite for T-05, T-06)  
**File:** `src/projects/Repo/MST/MST.cs`

Add two methods after implementing T-01:

```csharp
public async IAsyncEnumerable<INodeEntry> WalkReachableAsync()
// Per subtree: catch MissingBlockException, log it, skip branch, continue

public async IAsyncEnumerable<Leaf> ReachableLeavesAsync()
// Wraps WalkReachableAsync(), filters to Leaf entries only
```

Mirror `MST.walkReachable()` from `packages/repo/src/mst/mst.ts` lines 420–449.

---

### T-03: CID Computation Correctness
**Gap:** 12.15  
**Priority:** Must-have  
**File:** `src/pds_projects/ActorStore/Repo/Prepare.cs`

The `CidForSafeRecord()` method (line 82) does `JsonElement → CBOR → CID`. Verify byte-level equivalence against the canonical by:

1. Write a test that sends the same record JSON through both the canonical `cidForCbor` (via a reference test vector set from atproto) and `CidForSafeRecord`.
2. Identify where the JSON-to-CBOR conversion diverges (key ordering, type encoding, etc.).
3. Fix the CBOR serialization to produce DAG-CBOR compliant bytes (map keys must be sorted by UTF-8 byte order, all strings in UTF-8, integers in shortest form).

The fix likely requires using a deterministic DAG-CBOR encoder. The `PeterO.Cbor` library may not produce canonical DAG-CBOR by default — check and configure it, or replace with a deterministic encoder.

---

### T-04: CAR Import Verification
**Gap:** 12.8  
**Priority:** Must-have  
**File:** `src/atompds/Endpoints/Xrpc/Com/Atproto/Repo/ImportRepoEndpoints.cs`

Before `SqlRepoTransactor.PutManyAsync()`, add structural verification:

1. Parse the CAR header and validate roots (exactly 1 root = the commit CID).
2. Verify each block's CID matches its content: `Cid.FromBytes(block) == claimedCid`.
3. Verify the root commit CID is present in the block map.
4. Load the MST from the blocks and verify it is structurally valid (all referenced CIDs present, sorted order).
5. If the repo already has blocks, verify the incoming diff is a valid forward-only delta.
6. Enforce `PDS_ACCEPTING_REPO_IMPORTS` (gate the endpoint) and `PDS_MAX_REPO_IMPORT_SIZE` (stream size limit).

Add config vars to `ServerEnvironment.cs`:
```csharp
public bool PDS_ACCEPTING_REPO_IMPORTS { get; set; } = true;
public long PDS_MAX_REPO_IMPORT_SIZE { get; set; } = 100 * 1024 * 1024; // 100MB
```

---

### T-05: Repo Write Size Limit
**Gap:** 12.9  
**Priority:** Must-have  
**File:** `src/pds_projects/ActorStore/Repo/RepoRepository.cs`

In `FormatCommitAsync()`, after collecting `newBlocks` + `relevantBlocks` (once T-07 is done), enforce a 2MB cap:

```csharp
var totalSize = commit.NewBlocks.ByteSize() + (commit.RelevantBlocks?.ByteSize() ?? 0);
if (totalSize > 2 * 1024 * 1024)
    throw new XRPCError(new InvalidRequestErrorDetail("Commit too large"));
```

For now (before T-07), apply the limit only to `NewBlocks`.

---

### T-06: Lexicon Record Validation
**Gap:** 12.11  
**Priority:** Must-have  
**File:** `src/pds_projects/ActorStore/Repo/Prepare.cs`

Replace the `// TODO: need to properly validate the record` block (line 31) with real validation:

1. Build a static map of known lexicon schemas from the JSON files in `src/projects/CommonWeb/Lexicons/`.
2. For each create/update, verify `$type` exists in the schema map.
3. Validate required fields, field types, string formats (datetime, DID, handle, etc.) per lexicon.
4. Reject records with `createdAt` in the future by more than a configurable tolerance.
5. Set `ValidationStatus.Valid` when all checks pass; reject with `InvalidRequestError` when they fail.

A minimal implementation can validate the 5 most-written lexicons (`app.bsky.feed.post`, `app.bsky.actor.profile`, `app.bsky.graph.follow`, `app.bsky.feed.like`, `app.bsky.feed.repost`) and return `Unknown` for others rather than rejecting.

---

## Phase 2 — Protocol Compliance

These close protocol-level gaps that affect interop with the relay/indexer network.

---

### T-07: Covering Proofs & `RelevantBlocks`
**Gap:** 12.1, 12.2  
**Priority:** High  
**Files:** `src/projects/Repo/MST/MST.cs`, `src/projects/Repo/Types.cs`, `src/projects/Repo/Repo.cs`

**Step 1** — Add `RelevantBlocks` to `CommitData` in `Types.cs`:
```csharp
public record CommitData(Cid Cid, string Rev, string? Since, Cid? Prev,
    BlockMap NewBlocks, CidSet RemovedCids, BlockMap RelevantBlocks);
```

**Step 2** — Add `GetCoveringProofAsync(string key)` to `MST.cs`, mirroring `getCoveringProof` from `packages/repo/src/mst/mst.ts` lines 277–315:
- Walk from root to the leaf for `key`, collecting every MST node block encountered.
- For the leaf's parent node, also collect the left sibling and right sibling node blocks.
- Return a `BlockMap` of all collected node blocks.

**Step 3** — In `Repo.FormatCommitAsync()`, after computing `diff`, call `GetCoveringProofAsync` for each write key and add results to a `relevantBlocks` map. Pass it as the new `RelevantBlocks` field in `CommitData`.

**Step 4** — In `Sequencer.cs`, include `RelevantBlocks` when building `CommitEvt` so the firehose emits proof blocks.

---

### T-08: CAR Streamable Block Ordering
**Gap:** 12.7  
**Priority:** High  
**Files:** `src/projects/Repo/MST/MST.cs`, `src/pds_projects/ActorStore/Repo/SqlRepoTransactor.cs`

Add `CarBlockStreamAsync()` to `MST.cs`, mirroring `carBlockStream()` from `packages/repo/src/mst/mst.ts` lines 350–385:

```csharp
public async IAsyncEnumerable<CarBlock> CarBlockStreamAsync()
// Yields: commit block first, then MST nodes in pre-order DFS,
// interleaving leaf record blocks immediately after the node that contains them.
```

Update `GetRepoController` and the sync `getRepo` endpoint to use `CarBlockStreamAsync()` instead of the flat SQL-ordered `IterateCarBlocksAsync()`.

---

### T-09: Sync Verification Module
**Gap:** 12.6  
**Priority:** High  
**File:** `src/projects/Repo/Sync/Consumer.cs` (new)

Implement the six verification functions from `packages/repo/src/sync/consumer.ts`:

| Method | Description |
|---|---|
| `VerifyIncomingCarBlocksAsync(car)` | Check each block's CID matches its content |
| `VerifyRepoAsync(carBytes, did?, key?)` | Full CAR: signature, DID, MST structure |
| `VerifyRepoAsync(blocks, head, did?, key?)` | Block-map variant |
| `VerifyDiffAsync(repo, blocks, root, did?, key?)` | Validate incremental diff + operation inversion |
| `VerifyProofsAsync(proofs, claims, did, key)` | Validate individual Merkle proofs |
| `VerifyRecordsAsync(proofs, did, key)` | Extract and verify records from proofs |

This depends on T-07 (covering proofs) for `VerifyDiffAsync` and T-01 (typed errors) for `MissingBlockException`.

---

### T-10: Commit Re-signing (`resignCommit`)
**Gap:** 12.3  
**Priority:** Medium  
**File:** `src/projects/Repo/Repo.cs`

Add two methods matching the canonical:

```csharp
public async Task<CommitData> FormatResignCommitAsync(string rev, IKeyPair keypair)
// Load the commit for `rev`, create a new Commit with same `data` CID but new `sig`
// Do NOT increment rev — this is a signature-only change

public async Task<Repo> ResignCommitAsync(string rev, IKeyPair keypair)
// FormatResignCommit + ApplyCommit
```

Wire into the key rotation flow (`rotate-keys` admin script, T-19).

---

### T-11: Blob Constraints Enforcement
**Gap:** 12.12  
**Priority:** Medium  
**File:** `src/pds_projects/ActorStore/Repo/Prepare.cs`, `src/pds_projects/ActorStore/Repo/RepoRepository.cs`

1. In `ExtractBlobReferences()`, parse the lexicon field constraints (Accept MIME list, maxSize) from the parent object's `$type` + field name and populate `BlobConstraint.Accept` and `BlobConstraint.MaxSize`.
2. In `ProcessWritesAsync()`, after collecting blobs per write, look up each blob's metadata from `BlobTransactor.GetBlobAsync()` and verify:
   - `blob.MimeType` is in `constraint.Accept` (if Accept is non-empty).
   - `blob.Size <= constraint.MaxSize` (if MaxSize is set).
3. Throw `InvalidRequestError` with a descriptive message if constraints are violated.

---

### T-12: Content Type Sniffing for Blob Upload
**Gap:** 12.13  
**Priority:** Low  
**File:** `src/pds_projects/ActorStore/Repo/BlobTransactor.cs`

After writing the blob to temp storage, read the first 512 bytes back and run MIME sniffing (e.g., via `System.Net.Http.Headers.MediaTypeHeaderValue` + magic bytes comparison). Compare the sniffed MIME type against the client-declared MIME type and reject mismatches for high-risk types (HTML, SVG, JavaScript).

---

### T-13: Legacy Blob Reference Support
**Gap:** 12.14  
**Priority:** Low  
**File:** `src/pds_projects/ActorStore/Repo/Prepare.cs`

In `TryExtractBlobReference()`, add a second branch before the current `$type: blob` check:

```csharp
// Legacy: { "cid": "...", "mimeType": "..." } with no $type
if (!elem.TryGetProperty("$type", out _) &&
    elem.TryGetProperty("cid", out var cidElem) &&
    elem.TryGetProperty("mimeType", out var mimeElem))
{
    // parse and return PreparedBlobRef
}
```

---

### T-14: MST Structural Validation
**Gap:** 12.18  
**Priority:** Low  
**File:** `src/projects/Repo/MST/Util.cs`

In `DeserializeNodeData()`, add after key format validation:
1. Verify entries are in strict lexicographic (UTF-8 byte) ascending order.
2. Verify no node has more than 2048 entries (spec-recommended max).
3. Verify subtree pointers have `layer == currentNode.layer - 1` (depth consistency).
4. On block load, compute `CborBlock.Encode(data).Cid` and verify it equals the claimed CID.

---

### T-15: `searchAccounts` Admin Endpoint
**Gap:** 1.2  
**Priority:** Medium  
**File:** `src/atompds/Endpoints/Xrpc/Com/Atproto/Admin/SearchAccountsAdminEndpoints.cs` (new)

Implement `GET /xrpc/com.atproto.admin.searchAccounts`:
- Parameters: `email` (optional), `cursor` (optional), `limit` (default 50, max 100).
- Query: search `actor` + `account` join by email substring or handle substring.
- Return: `{ accounts: AccountView[], cursor? }`.
- Auth: admin token only.

Add `SearchAccountsAsync()` to `AccountStore.cs`.

---

## Phase 3 — Service Routing

Complete the proxy/routing layer to match canonical behavior.

---

### T-16: Ozone / Moderation Service Proxying
**Gap:** 2.1  
**Priority:** High  
**Files:** `src/atompds/Config/ServerEnvironment.cs`, `src/pds_projects/Config/`, `src/atompds/Endpoints/Xrpc/AppViewProxyEndpoints.cs`

**Step 1** — Add config:
```csharp
public string? PDS_MOD_SERVICE_URL { get; set; }
public string? PDS_MOD_SERVICE_DID { get; set; }
```

Create `ModServiceConfig.cs` in `src/pds_projects/Config/`.

**Step 2** — Add a new endpoint file `OzoneProxyEndpoints.cs` that registers routes for all `tools.ozone.*` NSIDs listed in GAPS.md §2.1. Route them to the mod service URL using service JWT auth (same pattern as `AppViewProxyEndpoints`).

**Step 3** — Add `tools.ozone.*` to the catch-all allowlist in `CatchallProxyAsync` once the explicit routes are registered.

**Step 4** — Implement `moderator` auth method in `AuthVerifier.cs` (see T-20).

---

### T-17: Dedicated Chat Service Routing
**Gap:** 2.2  
**Priority:** Medium  
**Files:** `src/atompds/Config/ServerEnvironment.cs`, `src/atompds/Endpoints/Xrpc/AppViewProxyEndpoints.cs`

**Step 1** — Add config:
```csharp
public string? PDS_CHAT_SERVICE_URL { get; set; }
public string? PDS_CHAT_SERVICE_DID { get; set; }
```

**Step 2** — In `AppViewProxyEndpoints.cs`, when resolving the proxy target for `chat.bsky.*` methods, prefer `PDS_CHAT_SERVICE_URL`/`PDS_CHAT_SERVICE_DID` over the app view.

**Step 3** — Replace the `chat.bsky.actor.deleteAccount` stub with a real proxy call to the chat service.

**Step 4** — Enforce `AppPassPrivileged` scope requirement when proxying `PRIVILEGED_METHODS` (all `chat.bsky.*`).

---

### T-18: Protected Method Enforcement
**Gap:** 2.3, 2.4  
**Priority:** Medium  
**File:** `src/atompds/Endpoints/Xrpc/AppViewProxyEndpoints.cs`

Define `PROTECTED_METHODS` set (matching the canonical 16-method list: session, email, identity, app password operations). In `CatchallProxyAsync`, before proxying, check if `nsid` is in this set and return `NotFound` immediately.

Define `PRIVILEGED_METHODS` set (chat + `createAccount`). In the proxy path, verify the token scope is `AppPassPrivileged` for these methods.

---

### T-19: Response Header Forwarding
**Gap:** 2.6  
**Priority:** Low  
**File:** `src/atompds/Endpoints/Xrpc/AppViewProxyEndpoints.cs`

In `InnerAsync()`, after receiving the upstream response, forward these headers to the client response if present:
```csharp
foreach (var header in new[] { "atproto-repo-rev", "atproto-content-labelers", "retry-after" })
{
    if (response.Headers.TryGetValues(header, out var vals))
        context.Response.Headers[header] = vals.ToArray();
}
```

---

## Phase 4 — Authentication

Close the auth method gaps to support Ozone and service-to-service auth.

---

### T-20: Moderator Auth & Service JWT Auth
**Gap:** 4.2, 4.3  
**Priority:** High (required for T-16)  
**File:** `src/atompds/Middleware/AuthVerifier.cs`

Add the following auth methods to `AuthVerifier`:

**`modService` auth:** Validates a Bearer JWT where `iss` is the configured mod service DID. Verifies the JWT signature against the mod service's current signing key (fetched from its DID document via `IdResolver`). Checks `lxm` claim matches the request NSID.

**`moderator` auth:** Dispatches to `modService` for Bearer tokens, or `adminToken` for Basic auth.

**`userServiceAuth` / `userServiceAuthOptional`:** Validates service JWTs from any DID. Verifies signature against the signing key from the issuer's DID document. Checks `lxm` claim.

**`authorizationOrUserServiceAuth`:** Tries OAuth Bearer, falls back to service JWT based on whether the token contains a `lxm` claim.

---

### T-21: Takendown Auth Scope
**Gap:** 4.6  
**Priority:** Low  
**File:** `src/atompds/Middleware/AuthVerifier.cs`

Add `Takendown` to `AuthScope` enum and `ScopeMap`:
```csharp
{AuthScope.Takendown, "com.atproto.takendown"}
```

When an account is taken down, issue tokens with the `Takendown` scope. Restrict taken-down accounts to only the `com.atproto.server.deleteAccount` endpoint. Update `AccessStandardAsync` to reject `Takendown` scope.

---

### T-22: Entryway PLC Rotation Key & Admin Token
**Gap:** 4.5  
**Priority:** Medium  
**Files:** `src/atompds/Config/ServerEnvironment.cs`, `src/atompds/Middleware/AuthVerifier.cs`

Add:
```csharp
public string? PDS_ENTRYWAY_PLC_ROTATION_KEY { get; set; }
public string? PDS_ENTRYWAY_ADMIN_TOKEN { get; set; }
```

Wire `PDS_ENTRYWAY_ADMIN_TOKEN` into the admin auth path — when the entryway admin token is configured and an admin request arrives with a matching token, treat it as authenticated. Wire `PDS_ENTRYWAY_PLC_ROTATION_KEY` into PLC operation signing.

---

## Phase 5 — Database & Persistence

Restore the commented-out tables and bring the schema to parity.

---

### T-23: Restore Commented-Out DB Tables
**Gap:** 7.1, 6.7, 6.8  
**Priority:** Medium  
**Files:** `src/pds_projects/AccountManager/Migrations/` (new migration)

Create migration `20260610000001_RestoreSecurityTables.cs` that adds:

- `used_refresh_token (id TEXT PK, did TEXT, usedAt DATETIME)` — for refresh token replay detection
- `device (id TEXT PK, accountDid TEXT, sessionId TEXT, createdAt DATETIME, lastSeenAt DATETIME)` — device tracking
- `account_device (did TEXT, deviceId TEXT, PRIMARY KEY (did, deviceId))` — device-account linking
- `authorization_request (id TEXT PK, did TEXT, parameters JSON, expiresAt DATETIME)` — OAuth PAR
- `authorized_client (id TEXT PK, did TEXT, clientId TEXT, scope TEXT, createdAt DATETIME)` — OAuth client tracking
- `token (id TEXT PK, did TEXT, tokenHash TEXT, createdAt DATETIME, expiresAt DATETIME)` — OAuth token store
- `lexicon (nsid TEXT PK, uri TEXT, cid TEXT, def JSON)` — lexicon cache

Wire `used_refresh_token` into `RefreshSession` to detect and block replayed refresh tokens.

---

### T-24: Persistent DID Cache
**Gap:** 6.1, 7.3  
**Priority:** Medium  
**Files:** `src/projects/Identity/`, `src/atompds/Config/ServerConfig.cs`

Create `SqliteDIDCache.cs` in `src/projects/Identity/` backed by `PDS_DID_CACHE_DB_LOCATION`:
- Schema: `did_cache (did TEXT PK, doc JSON, updatedAt DATETIME, staleAt DATETIME, expiresAt DATETIME)`
- Implement `IDidCache` with stale-while-revalidate: return stale entries but kick off background refresh.
- Register in `ServerConfig.cs` instead of `MemoryCache` when `PDS_DID_CACHE_DB_LOCATION` is set.

---

## Phase 6 — OAuth

Complete the OAuth implementation to match the canonical provider.

---

### T-25: OAuth Persistence (Sessions, Codes, Clients)
**Gap:** 3.1  
**Priority:** High  
**Files:** `src/atompds/Services/OAuth/OAuthSessionStore.cs`

Replace the in-memory `OAuthSessionStore` with SQLite-backed storage using the tables from T-23 (`authorization_request`, `authorized_client`, `token`):
- Persist authorization codes with expiry.
- Persist issued tokens with DPoP binding (`jkt`).
- Persist authorized client records.
- Survive server restarts.

---

### T-26: OAuth PAR (Pushed Authorization Requests)
**Gap:** 3.1, 3.2  
**Priority:** Medium  
**File:** `src/atompds/Endpoints/OAuth/` (new endpoint)

Add `POST /oauth/par` endpoint:
- Accepts standard OAuth authorization parameters in the POST body.
- Stores them in `authorization_request` table (from T-23).
- Returns a `request_uri` and `expires_in`.

Update `/.well-known/oauth-authorization-server` to advertise:
- `pushed_authorization_request_endpoint`
- `require_pushed_authorization_requests: true` (if configured)
- `dpop_signing_alg_values_supported: ["ES256", "ES256K"]`

---

### T-27: OAuth Well-Known Metadata Completion
**Gap:** 3.2  
**Priority:** Low  
**File:** `src/atompds/Endpoints/WellKnownEndpoints.cs`

Extend the `/.well-known/oauth-authorization-server` response to include:
- `client_id_metadata_document` URL
- `scopes_supported` with documentation links
- `pushed_authorization_request_endpoint`
- Branding fields (`primary_color`, `contrast_color`, etc.) from config

---

### T-28: DPoP Secret Configuration
**Gap:** 4.4  
**Priority:** Low  
**Files:** `src/atompds/Config/ServerEnvironment.cs`

Add:
```csharp
public string? PDS_DPOP_SECRET { get; set; }
```

Use it as a secondary HMAC key to validate DPoP proof `jti` values for replay prevention. Store seen `jti` values in Redis (or in-memory with TTL) to reject replays.

---

## Phase 7 — Rate Limiting & Configuration

---

### T-29: Rate Limit Bypass Mechanisms
**Gap:** 8.1  
**Priority:** Medium  
**Files:** `src/atompds/Config/ServerEnvironment.cs`, `src/atompds/Middleware/RateLimitMiddleware.cs`

Add:
```csharp
public string? PDS_RATE_LIMIT_BYPASS_KEY { get; set; }
public List<string> PDS_RATE_LIMIT_BYPASS_IPS { get; set; } = [];
```

In `RateLimitMiddleware`, before applying rate limits:
1. If `x-ratelimit-bypass` header matches `PDS_RATE_LIMIT_BYPASS_KEY`, skip limits.
2. If the request IP is in `PDS_RATE_LIMIT_BYPASS_IPS` (CIDR-aware), skip limits.

---

### T-30: Redis-Backed Rate Limiting
**Gap:** 8.2  
**Priority:** Medium  
**Files:** `src/atompds/Middleware/RateLimitMiddleware.cs`

When `PDS_REDIS_URL` is set, replace the in-memory `MemoryCache` sliding window with a Redis-backed implementation using `StackExchange.Redis`. Use atomic Lua scripts (`INCR` + `EXPIRE`) for distributed accuracy across multiple PDS instances.

Also add `PDS_REDIS_SCRATCH_PASSWORD` to `ServerEnvironment.cs` for Redis authentication.

---

### T-31: Remaining Missing Config Variables
**Gap:** 5.1, 5.2  
**Priority:** Low  
**File:** `src/atompds/Config/ServerEnvironment.cs`

Add the missing environment variables:
- `PDS_DPOP_SECRET` (see T-28)
- `PDS_RATE_LIMIT_BYPASS_KEY`, `PDS_RATE_LIMIT_BYPASS_IPS` (see T-29)
- `PDS_REDIS_SCRATCH_PASSWORD` (see T-30)
- `PDS_SQLITE_DISABLE_WAL_AUTO_CHECKPOINT`
- `PDS_HANDLE_BACKUP_NAMESERVERS`
- `PDS_MODERATION_EMAIL_SMTP_URL`, `PDS_MODERATION_EMAIL_ADDRESS`
- `PDS_HCAPTCHA_TOKEN_SALT`
- `PDS_LEXICON_AUTHORITY_DID`
- `LOG_LEVEL`, `LOG_DESTINATION`
- OAuth branding colors (`PDS_PRIMARY_COLOR`, `PDS_ERROR_COLOR`, `PDS_CONTRAST_SATURATION`, etc.)

Wire each into the appropriate service. For `PDS_INVITE_REQUIRED`, change the default to `true` to match canonical.

---

## Phase 8 — Infrastructure

---

### T-32: Blob Garbage Collection
**Gap:** 12.19  
**Priority:** Medium  
**Files:** `src/pds_projects/ActorStore/Repo/BlobTransactor.cs`, `src/atompds/Services/` (new background service)

Create `BlobGarbageCollectionService.cs` as a hosted background service:
- Runs on a configurable schedule (e.g., hourly).
- **Temp blob GC:** Delete temp blobs older than 24 hours that have no associated records.
- **Orphaned permanent blob GC:** Find blobs where no `record_blob` row exists (no active references) and delete from both DB and blob store.
- Use a distributed lock (Redis or SQLite advisory lock) to prevent concurrent GC runs.

---

### T-33: Separate Moderation Mailer
**Gap:** 6.3  
**Priority:** Low  
**Files:** `src/pds_projects/Mailer/`, `src/atompds/Config/ServerEnvironment.cs`

Create `ModerationMailer.cs` alongside `SmtpMailer.cs`. It reads from `PDS_MODERATION_EMAIL_SMTP_URL` and `PDS_MODERATION_EMAIL_ADDRESS` (add to `ServerEnvironment.cs`). If these are not set, fall back to the default mailer. Wire into moderation-related email sends.

---

### T-34: HTML Email Templates
**Gap:** 11.6  
**Priority:** Low  
**Files:** `src/pds_projects/Mailer/Templates/` (new directory)

Add Handlebars-style or Razor-based HTML email templates for:
- `confirm-email.html`
- `delete-account.html`
- `plc-operation.html`
- `reset-password.html`
- `update-email.html`

Update `SmtpMailer` to render HTML emails with a plain-text fallback part (multipart/alternative).

---

### T-35: Handle Backup Nameservers
**Gap:** 6.5  
**Priority:** Low  
**Files:** `src/atompds/Config/ServerEnvironment.cs`, `src/projects/Handle/HandleManager.cs`

Add:
```csharp
public List<string> PDS_HANDLE_BACKUP_NAMESERVERS { get; set; } = [];
```

In `HandleManager` DNS resolution, if the primary DNS lookup fails, retry against each configured backup nameserver in order before returning failure.

---

### T-36: Recovery & Maintenance Scripts
**Gap:** 6.2  
**Priority:** Medium  
**Files:** `src/pdsadmin/` or new project `src/pdstools/`

Implement the missing admin tools as subcommands of `pdsadmin` (or a new `pdstools` CLI):

| Command | Description |
|---|---|
| `rebuild-repo <did>` | Load all records for DID from DB, reconstruct MST, write new commit |
| `publish-identity <did>` | Emit an `identity` event for the DID to the sequencer |
| `rotate-keys <did>` | Verify the signing key in the DID document matches the local key; re-sign commit if needed (requires T-10) |
| `sequencer-recovery [--from <seq>]` | Replay all sequencer events from a given seq number |
| `recovery-repair-repos` | Find all repos that failed `rebuild-repo` and log them |

---

## Phase 9 — Deployment

---

### T-37: Docker & Compose Support
**Gap:** 10.1  
**Priority:** Medium  
**Files:** `Dockerfile`, `compose.yaml`, `.env.example`

Create:
- `Dockerfile` with a multi-stage build (SDK → runtime, .NET 10 Alpine base).
- `compose.yaml` with the PDS service + a Caddy reverse proxy for TLS termination.
- `.env.example` with all required and optional environment variables documented.
- Optional: `installer.sh` for interactive setup on a fresh Ubuntu/Debian VPS.

---

### T-38: Graceful Shutdown
**Gap:** 10.2  
**Priority:** Low  
**File:** `src/atompds/Services/BackgroundJobQueue.cs` (or wherever `BackgroundJobWorker` lives)

Implement `StopAsync(CancellationToken)` with explicit queue drain:
```csharp
public override async Task StopAsync(CancellationToken cancellationToken)
{
    _queue.CompleteAdding();
    await _processingTask.WaitAsync(cancellationToken);
}
```

Register SIGTERM → `IHostApplicationLifetime.StopApplication()`.

---

## Phase 10 — Test Coverage

---

### T-39: Missing Integration Test Areas
**Gap:** 9.1  
**Files:** `test/atompds.Tests/`

Add test files for the gaps in canonical coverage:

| Test File | Key Scenarios |
|---|---|
| `AccountMigrationTests.cs` | Export + import account between two PDS instances |
| `EntrywayTests.cs` | Entryway JWT auth, entryway admin token |
| `ModeratorAuthTests.cs` | Mod service JWT auth, moderator dispatch |
| `PlcOperationTests.cs` | Sign + submit PLC op, rotation key usage |
| `RaceConditionTests.cs` | Concurrent writes to the same repo, swap CID conflicts |
| `RateLimitTests.cs` | Rate limit hit, bypass key, bypass IP |
| `RecoveryTests.cs` | Run rebuild-repo, verify MST matches records |
| `TakedownAppealTests.cs` | Takedown → restricted scope → delete account |
| `DatabaseTests.cs` | Migration idempotency, schema validation |
| `ProxyViewTests.cs` | Read-after-write for each patched NSID |

---

## Execution Order Summary

```
Phase 1 (Security)   → T-01 → T-02 → T-03 → T-04 → T-05 → T-06
Phase 2 (Protocol)   → T-07 → T-08 → T-09 → T-10 → T-11 → T-12 → T-13 → T-14 → T-15
Phase 3 (Routing)    → T-16 → T-17 → T-18 → T-19
Phase 4 (Auth)       → T-20 → T-21 → T-22
Phase 5 (Database)   → T-23 → T-24
Phase 6 (OAuth)      → T-25 → T-26 → T-27 → T-28
Phase 7 (Rate Limit) → T-29 → T-30 → T-31
Phase 8 (Infra)      → T-32 → T-33 → T-34 → T-35 → T-36
Phase 9 (Deploy)     → T-37 → T-38
Phase 10 (Tests)     → T-39 (run alongside each phase)
```

**Hard dependencies:**
- T-02 requires T-01 (typed errors)
- T-07 requires T-01 (typed errors in diff computation)
- T-09 requires T-07 (covering proofs for diff verification)
- T-16 requires T-20 (moderator auth for Ozone endpoints)
- T-25 requires T-23 (DB tables for OAuth persistence)
- T-10 and T-36/rotate-keys depend on each other — T-10 first

**Estimated impact per phase:**

| Phase | Canonical Parity Gain |
|---|---|
| Phase 1 | Security/correctness — eliminates DoS risk and CID mismatch |
| Phase 2 | Protocol — enables relay verification and proper firehose |
| Phase 3 | Routing — enables Ozone integration |
| Phase 4 | Auth — enables mod service and service-to-service auth |
| Phase 5 | Persistence — eliminates in-memory state loss on restart |
| Phase 6 | OAuth — enables full OAuth flow for third-party apps |
| Phase 7 | Ops — enables distributed deployment |
| Phase 8 | Infra — prevents disk/memory exhaustion in production |
| Phase 9 | Deploy — enables self-hosted production deployment |
| Phase 10 | Quality — prevents regressions as parity work lands |
