# GAPS.md — Feature Gaps vs. Canonical PDS

Comparison of this .NET implementation (`blue-nile-pds`) against the canonical TypeScript PDS at [bluesky-social/atproto](https://github.com/bluesky-social/atproto/tree/main/packages/pds).

---

## 1. XRPC Endpoint Gaps

### 1.1 Deprecated Sync Endpoints (Missing)

| Endpoint | Status in Canonical | Status in Local |
|---|---|---|
| `com.atproto.sync.getCheckout` | Deprecated, still present | **Missing** |
| `com.atproto.sync.getHead` | Deprecated, still present | **Missing** |

These are deprecated but some relays/clients may still call them.

### 1.2 Missing Non-Deprecated Endpoint

| Endpoint | Status in Canonical | Status in Local |
|---|---|---|
| `com.atproto.admin.searchAccounts` | Present | **Present** (`Admin/SearchAccountsAdminEndpoints.cs`) |

All other non-deprecated `com.atproto.*` endpoints are present:
- `admin.*` (14 of 14 endpoints)
- `identity.*` (6 endpoints)
- `repo.*` (10 endpoints including `uploadBlob`, `importRepo`, `listMissingBlobs`)
- `server.*` (25 endpoints)
- `sync.*` (11 endpoints including `subscribeRepos`, `listRepos`, `getRepoStatus`)
- `moderation.*` (`createReport`)
- `temp.*` (`checkSignupQueue`)

---

## 2. Proxy / Service Routing Gaps

### 2.1 Ozone / Moderation Service Proxying — Done

The canonical PDS proxies **all `tools.ozone.*` methods** to a configured moderation service (`PDS_MOD_SERVICE_URL`). This includes:

- `tools.ozone.moderation.*` (11 methods: `emitEvent`, `getEvent`, `getRecord`, `getRepo`, `queryEvents`, `queryStatuses`, `scheduleAction`, `cancelScheduledActions`, `listScheduledActions`, `getAccountTimeline`, `searchRepos`)
- `tools.ozone.communication.*` (4 methods: `createTemplate`, `deleteTemplate`, `listTemplates`, `updateTemplate`)
- `tools.ozone.safelink.*` (5 methods: `addRule`, `queryEvents`, `queryRules`, `removeRule`, `updateRule`)
- `tools.ozone.team.*` (4 methods: `addMember`, `deleteMember`, `listMembers`, `updateMember`)
- `tools.ozone.verification.*` (3 methods: `grantVerifications`, `listVerifications`, `revokeVerifications`)

**Local:** `tools.ozone.*` methods are now proxied via a dedicated `OzoneProxyEndpoints.cs` which registers all 27 ozone routes and forwards them to the configured moderation service (`PDS_MOD_SERVICE_URL` / `PDS_MOD_SERVICE_DID`).

### 2.2 Chat Service Proxying — Incomplete

The canonical PDS proxies **14 `chat.bsky.*` methods** to a dedicated chat service via the `PRIVILEGED_METHODS` set. The local proxy routes `chat.bsky.*` through the catch-all to the app view (not a dedicated chat service), with the following specifics:

| Method | Local Status |
|---|---|
| `chat.bsky.actor.deleteAccount` | **Stub** (returns OK without proxying) |
| `chat.bsky.actor.exportAccountData` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.deleteMessageForSelf` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.getConvo` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.getConvoForMembers` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.getLog` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.getMessages` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.leaveConvo` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.listConvos` | **Proxied** (via static route) |
| `chat.bsky.convo.muteConvo` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.sendMessage` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.sendMessageBatch` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.unmuteConvo` | Proxied via catch-all (to app view, not chat service) |
| `chat.bsky.convo.updateRead` | Proxied via catch-all (to app view, not chat service) |

The missing piece is a dedicated `PDS_CHAT_SERVICE_URL`/`PDS_CHAT_SERVICE_DID` configuration and routing logic that sends chat methods to a separate chat service rather than the generic app view.

### 2.3 Protected Method Enforcement — Missing

The canonical PDS defines 16 `PROTECTED_METHODS` that must never be proxied (session, email, identity, app password operations). The local proxy has no such enforcement — the catch-all could theoretically proxy methods that should only be handled locally.

### 2.4 Privileged Method Enforcement — Missing

The canonical PDS marks chat methods and `createAccount` as `PRIVILEGED_METHODS` requiring `AppPassPrivileged` scope when proxied. The local implementation has no equivalent scope gating on proxied requests.

### 2.5 CDN URL Pattern — Stored but Not Used

`PDS_BSKY_APP_VIEW_CDN_URL_PATTERN` is defined in config and stored in `BskyAppViewConfig`, but is never consumed at runtime. The canonical PDS uses it to rewrite blob/image URLs to CDN endpoints for better performance.

### 2.6 Response Header Forwarding — Incomplete

The canonical proxy forwards these response headers from upstream services:
- `atproto-repo-rev`
- `atproto-content-labelers`
- `retry-after`

The local proxy does not forward these headers.

### 2.7 Content Encoding Negotiation — Missing

The canonical PDS negotiates content encoding (gzip/deflate) for proxied responses based on `PDS_PROXY_PREFER_COMPRESSED` config. The local implementation does not implement content encoding negotiation.

---

## 3. OAuth Gaps

### 3.1 OAuth Provider — Basic vs. Full

The canonical PDS uses the full `@atproto/oauth-provider` library, which provides:
- **Stateful access tokens** with DPoP binding
- **Authorization request persistence** (stored in SQLite `authorization_request` table)
- **Authorized client tracking** (stored in SQLite `authorized_client` table)
- **Device/account association** (`device` and `account_device` tables)
- **Lexicon-based scope resolution** via `LexResolver`
- **hCaptcha integration** in the OAuth flow (token salt, verification)
- **Full branding** (colors, logos, i18n links with `rel` types)
- **OAuth PAR (Pushed Authorization Requests)** support
- **Token introspection** support
- **DPoP nonce management**

The local implementation provides a basic PKCE flow with in-memory `OAuthSessionStore`:
- Authorizations expire in 10 minutes (in-memory only)
- Codes expire in 1 minute (in-memory only)
- No persistence across restarts
- No authorized client tracking
- No device management
- No scope resolution via lexicon
- No OAuth branding customization
- No PAR support

### 3.2 OAuth Well-Known Metadata — Incomplete

The canonical `/.well-known/oauth-authorization-server` returns extensive metadata including:
- `dpop_signing_alg_values_supported` (includes `ES256K` in addition to `ES256`)
- `client_id_metadata_document` with full URL
- `scopes_supported` with documentation links
- Service branding (colors, logos, links)
- `pushed_authorization_request_endpoint`
- `require_pushed_authorization_requests`

The local implementation returns basic metadata only (issuer, endpoints, supported grants/challenges).

### 3.3 OAuth Client Metadata Endpoint — Simplified

The canonical `oauth/client-metadata.json` endpoint resolves client metadata by fetching the client's metadata document. The local implementation either redirects to the entryway or simply redirects to the `client_id` URL, without proper client metadata resolution.

---

## 4. Authentication & Authorization Gaps

### 4.1 Admin Password — Dev-Mode Default Only

The canonical PDS reads `PDS_ADMIN_PASSWORD` from environment and requires it to be set. The local implementation reads `PDS_ADMIN_PASSWORD` from environment but **defaults to `"secret"` when `PDS_DEV_MODE=true`**. In production mode (`PDS_DEV_MODE=false`), the server throws an exception at startup if `PDS_ADMIN_PASSWORD` is not set. This is functionally safe for production but differs from canonical in allowing a default in DevMode.

### 4.2 Moderator Auth Scope — Missing

The canonical PDS has:
- `modService` auth method — validates service JWTs from the configured moderation service DID
- `moderator` auth method — dispatches to either `modService` (Bearer) or `adminToken` (Basic auth)
- This allows the Ozone moderation service to make authenticated admin-level requests

The local implementation has no moderator or mod-service authentication concept.

### 4.3 Service Auth Methods — Missing

The canonical `AuthVerifier` supports multiple auth modes that the local doesn't:
- `userServiceAuth` — validates service JWTs (checks `lxm` claim, verifies signing key from DID document)
- `userServiceAuthOptional` — same but optional
- `authorizationOrUserServiceAuth` — dispatches between OAuth and service auth based on `lxm` claim
- `authorizationOrAdminTokenOptional` — tries auth, falls back to admin or unauthenticated
- `unauthenticated` — rejects if Authorization header is present (for public endpoints)

### 4.4 DPoP Secret — Missing

The canonical PDS has `PDS_DPOP_SECRET` for additional DPoP proof validation. The local implementation has no shared DPoP secret.

### 4.5 Entryway Auth — Partial

The canonical PDS supports entryway configuration with:
- `PDS_ENTRYWAY_URL`
- `PDS_ENTRYWAY_DID`
- `PDS_ENTRYWAY_JWT_VERIFY_KEY_K256_PUBLIC_KEY_HEX`
- `PDS_ENTRYWAY_PLC_ROTATION_KEY`
- `PDS_ENTRYWAY_ADMIN_TOKEN`

The local implementation has the first three (as `PDS_OAUTH_ENTRYWAY_*`) but is missing:
- `PDS_ENTRYWAY_PLC_ROTATION_KEY` — needed for PLC operations through the entryway
- `PDS_ENTRYWAY_ADMIN_TOKEN` — needed for admin operations through the entryway

### 4.6 Takendown Scope — Missing

The canonical PDS defines a `Takendown` auth scope (`com.atproto.takendown`) for taken-down accounts. The local implementation checks takedown status but doesn't use a dedicated scope.

---

## 5. Configuration Gaps

### 5.1 Missing Environment Variables

| Canonical Variable | Default | Purpose | Local Status |
|---|---|---|---|
| `PDS_DPOP_SECRET` | (none) | DPoP proof secret | **Missing** |
| `PDS_RATE_LIMIT_BYPASS_KEY` | (none) | Bypass key for rate limits | **Present** (`ServerEnvironment.cs:122`) |
| `PDS_RATE_LIMIT_BYPASS_IPS` | (none) | Bypass IPs for rate limits | **Present** (`ServerEnvironment.cs:123`) |
| `PDS_REDIS_SCRATCH_PASSWORD` | (none) | Redis password | **Missing** (has `PDS_REDIS_URL` only) |
| `PDS_SQLITE_DISABLE_WAL_AUTO_CHECKPOINT` | `false` | Disable WAL auto-checkpoint | **Missing** |
| `PDS_HANDLE_BACKUP_NAMESERVERS` | (none) | Backup DNS nameservers | **Missing** |
| `PDS_MODERATION_EMAIL_SMTP_URL` | (none) | Separate moderation SMTP | **Missing** |
| `PDS_MODERATION_EMAIL_ADDRESS` | (none) | Moderation sender address | **Missing** |
| `PDS_ACCEPTING_REPO_IMPORTS` | (none) | Allow account imports | **Present** (`ServerEnvironment.cs:185`) |
| `PDS_MAX_REPO_IMPORT_SIZE` | (none) | Max import size | **Present** (`ServerEnvironment.cs:186`) |
| `PDS_HCAPTCHA_TOKEN_SALT` | (none) | hCaptcha token salt | **Missing** |
| `PDS_ENTRYWAY_PLC_ROTATION_KEY` | (none) | Entryway PLC key | **Missing** |
| `PDS_ENTRYWAY_ADMIN_TOKEN` | (none) | Entryway admin token | **Missing** |
| `PDS_MOD_SERVICE_URL` | (none) | Ozone mod service URL | **Present** (`ServerEnvironment.cs:158`) |
| `PDS_MOD_SERVICE_DID` | (none) | Ozone mod service DID | **Present** (`ServerEnvironment.cs:159`) |
| `PDS_LEXICON_AUTHORITY_DID` | (none) | Lexicon authority DID | **Missing** |
| `LOG_LEVEL` | `info` | PDS-specific log level | **Missing** |
| `LOG_DESTINATION` | (stderr) | PDS-specific log destination | **Missing** |
| `PDS_PRIMARY_COLOR` (+ contrast, hue) | (none) | OAuth branding | **Missing** |
| `PDS_ERROR_COLOR`, `WARNING_COLOR`, etc. | (none) | OAuth theme colors | **Missing** |
| `PDS_CONTRAST_SATURATION` | (none) | Branding saturation | **Missing** |

### 5.2 Config Structural Differences

| Aspect | Canonical | Local |
|---|---|---|
| SMTP config | Single `PDS_EMAIL_SMTP_URL` (supports smtp://, smtps://, sendmail://) | Separate `PDS_SMTP_HOST/PORT/USERNAME/PASSWORD`; TLS hardcoded to `SecureSocketOptions.StartTls` |
| Invite required default | `true` | `false` |
| Blob upload limit naming | `PDS_BLOB_UPLOAD_LIMIT` | `PDS_BLOB_UPLOAD_LIMIT_IN_BYTES` |

---

## 6. Services & Infrastructure Gaps

### 6.1 DID Cache — In-Memory vs. Persistent

The canonical PDS uses a separate SQLite database (`did_cache.sqlite`) for DID resolution caching with stale-while-revalidate semantics. The local implementation uses an in-memory `ConcurrentDictionary` (`MemoryCache` in `src/projects/Identity/`). This means:
- Cache is lost on restart
- No persistence across instances
- Memory grows unbounded (within TTL constraints)

### 6.2 Recovery / Maintenance Scripts — Missing

The canonical PDS includes several critical maintenance scripts:

| Script | Purpose |
|---|---|
| `rebuild-repo` | Rebuild a repo's MST from record table data |
| `publish-identity` | Publish identity events on firehose |
| `rotate-keys` | Ensure PLC signing key matches local key |
| `sequencer-recovery` | Replay sequencer for full data recovery |
| `recovery-repair-repos` | Rebuild repos that failed during recovery |

The local implementation has **none** of these. Only the migration utility exists.

### 6.3 Separate Moderation Mailer — Missing

The canonical PDS has a separate `ModerationMailer` with its own SMTP configuration (`PDS_MODERATION_EMAIL_SMTP_URL`). This allows moderation emails to be sent through a different email infrastructure. The local implementation has a single mailer.

### 6.4 Image Processing — Missing

The canonical PDS has an `image/` module with `ImageUrlBuilder` for constructing CDN-optimized image URLs (thumbnail generation, format conversion). The local implementation has no image processing capabilities.

### 6.5 Handle Backup Nameservers — Missing

The canonical PDS supports `PDS_HANDLE_BACKUP_NAMESERVERS` for fallback DNS resolution during handle verification. This improves reliability when primary DNS is unavailable. The local implementation has no backup nameserver support.

### 6.6 Read-After-Write — Partial

The canonical PDS has a full `read-after-write/` module that:
- Intercepts proxied responses
- Buffers the response for local inspection
- Patches records/collections that were recently written
- Handles content encoding during patching
- Supports a `LocalViewer` for viewing records without proxying

The local implementation has `WriteSnapshotCache` which tracks recent writes and patches proxied responses. It patches `cid`, `record`, `value`, `displayName`, `description`, and `name` fields by substituting locally-cached data. However:
- Doesn't handle content encoding
- Doesn't buffer/proxy responses for inspection
- `LocalViewer` pattern for bypassing the proxy entirely is missing

### 6.7 Account Device Tracking — Missing

The canonical PDS tracks devices (`device`, `account_device` tables) for security auditing and session management. The local implementation has these tables commented out in the account manager migration.

### 6.8 Used Refresh Token Tracking — Missing

The canonical PDS tracks used refresh tokens in a `used_refresh_token` table to detect token replay attacks. The local implementation has this table commented out.

### 6.9 Lexicon Resolver — Missing

The canonical PDS has a `LexiconResolver` for dynamically resolving lexicon definitions at runtime. This is used for OAuth scope resolution and validation. The local implementation has no lexicon resolver.

### 6.10 Scope Reference Getter — Missing

The canonical PDS uses `ScopeReferenceGetter` to dereference OAuth scopes via the entryway when configured. This allows the entryway to manage which scopes are available. The local implementation has no scope dereferencing.

---

## 7. Database / Persistence Gaps

### 7.1 Account Manager Schema

| Canonical Table | Local Status |
|---|---|
| `account` | Present |
| `actor` | Present |
| `refresh_token` | Present |
| `app_password` | Present |
| `repo_root` | Present |
| `invite_code` | Present |
| `invite_code_use` | Present |
| `email_token` | Present |
| `account_device` | **Commented out** |
| `authorization_request` | **Commented out** |
| `authorized_client` | **Commented out** |
| `device` | **Commented out** |
| `lexicon` | **Missing** |
| `token` | **Commented out** |
| `used_refresh_token` | **Commented out** |

### 7.2 Migration Count

The canonical PDS has **7 account manager migrations** (001-init through 007). The local implementation has **2** (Init + EmailToken). This means the local schema is at an earlier evolution stage.

### 7.3 DID Cache Database — Missing

The canonical PDS uses a separate SQLite database (`did_cache.sqlite`) for DID caching. The local implementation has the config property `PDS_DID_CACHE_DB_LOCATION` but uses an in-memory cache instead — the database is never created or used.

---

## 8. Rate Limiting Gaps

### 8.1 Bypass Mechanisms — Missing

The canonical PDS supports:
- `PDS_RATE_LIMIT_BYPASS_KEY` — requests with `x-ratelimit-bypass` header matching this key skip rate limiting
- `PDS_RATE_LIMIT_BYPASS_IPS` — list of IPs/CIDRs that bypass rate limiting

The local implementation has no bypass mechanisms.

### 8.2 Redis-Backed Rate Limiting — Missing

The canonical PDS uses Redis for distributed rate limiting when configured. The local implementation uses ASP.NET Core's in-memory sliding window rate limiter only, regardless of Redis configuration.

---

## 9. Testing Gaps

### 9.1 Test Coverage

The local implementation now has 5 test projects with 27+ integration test files in `atompds.Tests` covering:
- Account management (creation, deactivation, deletion)
- Admin operations and lifecycle
- App passwords
- Blob operations
- CRUD operations
- Email confirmation
- Handle management and identity
- Invite codes
- Moderation and temp endpoints
- OAuth flows
- Passwords and auth scopes
- Proxy behavior
- Repo operations
- Root endpoints and health
- Sequencer
- Sync and federation

Missing vs. canonical tests:
| Area | Canonical Tests | Local Tests |
|---|---|---|
| Account migration | `account-migration.test.ts` | **None** |
| Entryway | `entryway.test.ts` | **None** |
| Moderator auth | `moderator-auth.test.ts` | **None** |
| PLC operations | `plc-operations.test.ts` | **None** |
| Race conditions | `races.test.ts` | **None** |
| Rate limiting | `rate-limits.test.ts` | **None** |
| Recovery | `recovery.test.ts` | **None** |
| Takedown appeal | `takedown-appeal.test.ts` | **None** |
| Database | `db.test.ts` | **None** |
| Proxy views | 8 proxied test files | **None** |

**Total canonical:** ~40+ test files with comprehensive integration tests. **Local:** 5 test projects with ~30 test files covering most core flows.

---

## 10. Deployment Gaps

### 10.1 Container / Docker Support — Partial

The canonical PDS provides:
- `Dockerfile` with multi-stage build
- `compose.yaml` with Caddy reverse proxy + automatic TLS
- `installer.sh` for interactive VPS setup
- ACME/Let's Encrypt certificate automation

The local implementation now has:
- `Dockerfile` — multi-stage build using `mcr.microsoft.com/dotnet/sdk:10.0-preview-alpine`
- `compose.yaml` — defines `pds` and `caddy` services with persistent volumes
- `Caddyfile` — minimal reverse proxy configuration

Missing vs. canonical: `installer.sh`, ACME certificate automation, production-ready health checks.

### 10.2 Graceful Shutdown — Missing

The canonical PDS implements graceful shutdown via SIGTERM handling with queue draining. The local `BackgroundJobWorker` does not implement `IHostedService.StopAsync` with explicit drain logic (relies on ASP.NET Core's default `BackgroundService` cancellation).

---

## 11. Additional Minor Gaps

### 11.1 Landing Page — Different

The canonical PDS serves ASCII art at `/` with links to code and documentation. The local implementation returns a JSON service info object.

### 11.2 robots.txt — Different

The canonical PDS allows all crawling (`Allow: /`). The local implementation restricts to `Allow: /xrpc/` and `Disallow: /`.

### 11.3 Import Repo — No Size Limit

The canonical PDS has `PDS_MAX_REPO_IMPORT_SIZE` and `PDS_ACCEPTING_REPO_IMPORTS` to control repo imports. The local `ImportRepoController` reads the entire body into memory with no size limit.

### 11.4 Logging — Standard vs. Custom

The canonical PDS uses Pino with configurable `LOG_LEVEL` and `LOG_DESTINATION`. The local implementation uses standard ASP.NET Core logging with `appsettings.json` defaults.

### 11.5 `PDS_SERVICE_DID` Default

The canonical PDS defaults `PDS_SERVICE_DID` to `did:web:{hostname}`. The local implementation requires explicit configuration or falls back to constructing it from the hostname.

### 11.6 Email Templates

The canonical PDS uses Handlebars templates for emails (`confirm-email.hbs`, `delete-account.hbs`, `plc-operation.hbs`, `reset-password.hbs`, `update-email.hbs`). The local `SmtpMailer` sends plain-text emails only.

---

## Summary Statistics

| Category | Canonical | Local | Gap |
|---|---|---|---|---|
| XRPC endpoints (non-deprecated) | ~65 | ~65 | 0 missing |
| Deprecated endpoints | 2 | 0 | 2 missing |
| Proxy targets (ozone/chat/report) | Full | Partial (ozone done, chat proxied to appview, no dedicated service) | Moderate |
| Repo/MST function parity (61 functions) | Full | 54 equiv | 7 missing (11%) |
| OAuth features | Full provider | Basic PKCE | Major |
| Auth scopes/methods | 10+ modes | 5 modes | Significant |
| Config variables | ~90+ | ~76 | ~14 missing |
| Account DB tables | 15 | 9 | 6 missing/commented |
| Test files | ~40+ | ~30 | ~10 missing |
| Repo/MST functions | 61 | 54 | 7 missing (car stream, walkRecords, readable blockstore, sync storage, verifyIncomingCarBlocks, blob constraints, legacy refs) |
| Recovery scripts | 6 | 0 | All missing |
| Deployment tooling | Docker+Caddy | Partial (Docker + compose + Caddy) | Installer script, ACME automation |

---

## 12. Repository & MST Gaps

Comparison of the core repository data structure implementation (Merkle Search Tree, commits, CAR) against the [AT Protocol Repository Spec](https://atproto.com/specs/repository) and the reference `@atproto/repo` package.

### 12.4 Read-Only Repo Abstraction — Missing

| Aspect | Detail |
|---|---|
| **Spec reference** | — (architectural best practice) |
| **Reference** | `ReadableRepo` class in `packages/repo/src/readable-repo.ts` (read-only walk/query operations). `ReadableBlockstore` abstract class in `packages/repo/src/storage/readable-blockstore.ts`. `SyncStorage` in `packages/repo/src/storage/sync-storage.ts` — two-tier storage (staged writes + persisted reads) for verification workflows |
| **Local** | Single `Repo` class (in `Repo.cs`) with both read and write methods. Single `IRepoStorage` interface (in `IRepoStorage.cs`) for all storage operations. No `ReadableRepo` or `ReadableBlockstore` equivalent |
| **Impact** | Verification workflows (which need read-only access to staged+persisted data) cannot cleanly separate concerns. Code that only needs to read the repo must accept a full read/write interface |
| **Priority** | Low |

### 12.7 CAR Streamable Block Ordering — Not Compliant

| Aspect | Detail |
|---|---|
| **Spec reference** | Streamable CAR Block Ordering: "commit object must be the first block. MST nodes are included in 'pre-order'... Following each MST node in the tree, include the blocks corresponding to the entries in that node" |
| **Reference** | `MST.carBlockStream()` in `packages/repo/src/mst/mst.ts` — yields blocks in spec-compliant order: commit first, then MST nodes in pre-order (parent before children, depth-first), with leaf blocks interleaved after their parent MST nodes |
| **Local** | `SqlRepoTransactor.IterateCarBlocksAsync()` in `SqlRepoTransactor.cs` emits blocks ordered by `(repoRev DESC, cid DESC)` — a flat storage-level pagination with no awareness of the MST tree structure. `GetRepoController.cs` uses this to stream CAR responses |
| **Impact** | CAR exports from this PDS are not in streamable ordering. Clients must buffer all blocks in memory to reconstruct the MST, defeating the memory-efficiency purpose of streamable CAR |
| **Priority** | Medium |

### 12.12 Blob Constraints Enforcement — Not Enforced

| Aspect | Detail |
|---|---|
| **Spec reference** | Blob spec: blob references include `accept` (MIME whitelist) and `maxSize` constraints per lexicon field |
| **Reference** | PDS enforces per-blob constraints during `processWrites()` — validates blob MIME type is in the allowed set and blob size is under the per-constraint maximum |
| **Local** | `Prepare.ExtractBlobReferences()` extracts constraint info into `BlobConstraint` records but does **not enforce them**. Line 194: `// TODO: there is a constraints stuff they extract in the reference implementation`. Line 307: `// TODO: constraints` |
| **Impact** | Blobs outside the allowed MIME type or size constraints can be attached to records that explicitly restrict them (e.g., a 100MB "image" attached to a profile avatar field) |
| **Priority** | Medium |

### 12.13 Content Type Sniffing — Missing

| Aspect | Detail |
|---|---|
| **Spec reference** | Blob spec: PDS should verify uploaded blob content matches declared MIME type |
| **Reference** | PDS verifies uploaded blob content type against declared MIME type |
| **Local** | `BlobTransactor.cs`: the uploaded MIME type from the client is accepted as-is without content sniffing |
| **Impact** | Clients can declare a blob as one type (e.g., `image/jpeg`) while uploading content of a different type (e.g., `text/html`), bypassing content type restrictions |
| **Priority** | Low |

### 12.14 Legacy Blob Format — Not Supported

| Aspect | Detail |
|---|---|
| **Spec reference** | Blob spec: legacy `$type: blob` reference format |
| **Reference** | PDS supports legacy blob reference format alongside the current `$type: blob` with `ref.$link` |
| **Local** | `Prepare.cs` line 246: `// TODO: maybe support legacy blob format`. Only the current blob format is supported |
| **Impact** | Records created by older clients using legacy blob format will have blob references that cannot be processed |
| **Priority** | Low |

### 12.16 Blob Post-Transaction Inconsistency — Acknowledged

| Aspect | Detail |
|---|---|
| **Spec reference** | — (operational reliability) |
| **Reference** | PDS handles blob operations within the actor store transaction context, ensuring atomicity |
| **Local** | `BlobController.cs` line 139: "there is a chance of failure here after the transaction, status in db might be inconsistent with actual blob storage". If blob storage operation succeeds but the process crashes before the DB transaction commits (or vice versa), the blob status in DB will not match the blob store |
| **Impact** | Orphaned blob records or phantom blobs on restart. Recovery requires manual reconciliation |
| **Priority** | Medium |

### 12.17 Backlink Conflict Detection — Silent

| Aspect | Detail |
|---|---|
| **Spec reference** | — (data integrity) |
| **Reference** | `RecordReader.getBacklinkConflicts(uri, record)` in `packages/pds/src/actor-store/record/reader.ts` — detects duplicate likes, reposts, follows by checking existing backlinks before creating new ones. Returns conflicts to the caller for proper error handling |
| **Local** | `RecordRepository.AddBacklinksAsync()` in `RecordRepository.cs` silently skips conflicts (`ON CONFLICT DO NOTHING`). No detection or reporting of duplicate interactions |
| **Impact** | Duplicate likes/reposts/follows are silently accepted by the PDS but may not be reflected in downstream views. Client receives a success response but the duplicate may not actually be indexed |
| **Priority** | Medium |

### 12.18 MST Structural Validation — Minimal

| Aspect | Detail |
|---|---|
| **Spec reference** | Security Considerations: "verif[y] depth and sort order of keys... limit the number of TreeEntries per Node" |
| **Reference** | MST loader validates structure — verifies entries ordering, depth consistency, key format. `verifyIncomingCarBlocks()` validates CID→content integrity |
| **Local** | `DeserializeNodeData()` validates key format (`EnsureValidMstKey`) but does not validate: depth consistency across the tree, entry count limits per node, deterministic ordering of entries, CID content integrity on load |
| **Impact** | Corrupt or maliciously crafted MST structures may be accepted during import or normal operation, potentially causing resource exhaustion or incorrect tree behavior |
| **Priority** | Low |

### 12.20 `Commit.since` Handling — Verify Correctness

| Aspect | Detail |
|---|---|
| **Spec reference** | Repository Diffs: `since` field indicates the previous revision for differential sync |
| **Reference** | `CommitData.since` is populated with the previous commit rev during `formatCommit()`. Used for differential CAR export and sync protocol |
| **Local** | `CommitData.Since` field exists in `Types.cs`. Should be populated during diff computation but verify propagation: reference sets `since: prevCommit?.rev ?? null` inside `formatCommit()`. C# `Repo.FormatCommitAsync()` should be checked for correct population |
| **Impact** | If `since` is incorrectly populated, differential sync (`getRepo?since=...`) may produce incorrect diffs |
| **Priority** | Medium |

---
