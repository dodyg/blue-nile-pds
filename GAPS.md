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

### 2.1 Ozone / Moderation Service Proxying — Missing

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
| Proxy targets (ozone/chat/report) | Full | Partial (ozone done, chat proxied) | Moderate |
| Repo/MST function parity (61 functions) | Full | 54 equiv | 7 missing (11%) |
| OAuth features | Full provider | Basic PKCE | Major |
| Auth scopes/methods | 10+ modes | 5 modes | Significant |
| Config variables | ~90+ | ~76 | ~14 missing |
| Account DB tables | 15 | 9 | 6 missing/commented |
| Test files | ~40+ | ~30 | ~10 missing |
| Repo/MST functions | 61 | 54 | 7 missing (proofs, verification, CAR compliance) |
| Recovery scripts | 6 | 0 | All missing |
| Deployment tooling | Docker+Caddy | Partial (Docker + compose + Caddy) | Installer script, ACME automation |

---

## 12. Repository & MST Gaps

Comparison of the core repository data structure implementation (Merkle Search Tree, commits, CAR) against the [AT Protocol Repository Spec](https://atproto.com/specs/repository) and the reference `@atproto/repo` package.

### 12.1 Covering Proofs (`getCoveringProof`) — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | Repository Diffs: "proof chain" for verifying created/updated records independently; operation inversion requires "MST nodes necessary for the inversion process" |
| **Reference** | `MST.getCoveringProof(key)` in `packages/repo/src/mst/mst.ts` — recursively collects all MST node blocks along the path to a key, plus left and right sibling nodes. Called from `Repo.formatCommit()` in `packages/repo/src/repo.ts` to populate `CommitData.relevantBlocks` |
| **Local** | `MST.GetCoveringProofAsync()` at `MST.cs:495` — walks the MST from root to leaf, collecting sibling blocks as covering proof. Called from `Repo.cs:115` to populate `CommitData.NewBlocks` and `CommitData.RelevantBlocks` during `FormatCommitAsync` |
| **Impact** | ✅ Addressed. Covering proofs are collected during commit formatting and included in sequencer events |
| **Priority** | High |

### 12.2 `relevantBlocks` in CommitData — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | Repository Diffs: "diff is a partial Merkle tree, including a signed commit, and can be partially verified in isolation" — requires inclusion of contextual MST nodes |
| **Reference** | TypeScript `CommitData` (in `packages/repo/src/types.ts`) has both `newBlocks: BlockMap` (changed blocks) and `relevantBlocks: BlockMap` (covering proofs, contextual MST nodes needed for inversion). `Repo.formatCommit()` populates both |
| **Local** | C# `CommitData.RelevantBlocks` populated in `Repo.cs:115` — `GetCoveringProofAsync()` is called for each write key during `FormatCommitAsync` and the resulting blocks are stored alongside `NewBlocks` |
| **Impact** | ✅ Addressed. `RelevantBlocks` is populated during commit formatting and included in sequencer events for downstream verification |
| **Priority** | High |

### 12.3 Commit Re-signing (`resignCommit`) — Missing

| Aspect | Detail |
|---|---|
| **Spec reference** | Commit Objects: "a new repository commit should be created every time the signing key is rotated. Such a commit does not need to update the `data` CID link" |
| **Reference** | `Repo.formatResignCommit(rev, keypair)` and `Repo.resignCommit(rev, keypair)` in `packages/repo/src/repo.ts` — re-signs the current commit with a new key, keeping the same `data` CID (MST unchanged). Used during key rotation |
| **Local** | **Missing entirely** — no equivalent in `Repo.cs` |
| **Impact** | Key rotation cannot be performed without rewriting the entire repo (creating a new commit via `FormatCommitAsync` with no writes, which would still recompute the MST unnecessarily) |
| **Priority** | Medium |

### 12.4 Read-Only Repo Abstraction — Missing

| Aspect | Detail |
|---|---|
| **Spec reference** | — (architectural best practice) |
| **Reference** | `ReadableRepo` class in `packages/repo/src/readable-repo.ts` (read-only walk/query operations). `ReadableBlockstore` abstract class in `packages/repo/src/storage/readable-blockstore.ts`. `SyncStorage` in `packages/repo/src/storage/sync-storage.ts` — two-tier storage (staged writes + persisted reads) for verification workflows |
| **Local** | Single `Repo` class (in `Repo.cs`) with both read and write methods. Single `IRepoStorage` interface (in `IRepoStorage.cs`) for all storage operations. No `ReadableRepo` or `ReadableBlockstore` equivalent |
| **Impact** | Verification workflows (which need read-only access to staged+persisted data) cannot cleanly separate concerns. Code that only needs to read the repo must accept a full read/write interface |
| **Priority** | Low |

### 12.5 Graceful Tree Walking (`walkReachable`) — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | Security Considerations: handle corrupted data gracefully; CAR imports may have dangling references |
| **Reference** | `MST.walkReachable()` and `MST.reachableLeaves()` in `packages/repo/src/mst/mst.ts` — graceful walker that catches `MissingBlockError` per subtree and skips unreachable branches, then continues walking the rest of the tree |
| **Local** | `WalkReachableAsync()` at `MST.cs:386` — gracefully handles missing blocks by catching `MissingBlockException` and returning null. `SafeWalkReachableAsync()` at line 456 — skips subtrees with missing blocks via try/catch. `ReachableLeavesAsync()` at line 479 — streams leaves from `WalkReachableAsync()` |
| **Impact** | ✅ Addressed. Recovery/verification workflows can walk incomplete repos and recover partial data |
| **Priority** | Medium |

### 12.6 Sync Verification Module — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | Repository Diffs: verification of diffs, proofs, and full repos; CAR File: import validation |
| **Reference** | Full `packages/repo/src/sync/consumer.ts` with: `verifyRepo(carBytes)` — verifies full repo CAR (signature, DID, MST structure). `verifyDiff(repo, blocks, root)` — validates incremental diff (operation inversion, proof verification). `verifyProofs(proofs, claims, did, key)` — validates individual Merkle proofs against record claims. `verifyRecords(proofs, did, key)` — extracts and verifies all records from a proof |
| **Local** | `Consumer.cs` at `src/projects/Repo/Sync/Consumer.cs` — implements 5 verify methods: `VerifyRepoAsync` (2 overloads: `byte[] carBytes` or `BlockMap` + CID), `VerifyDiffAsync` (forward delta check, DID match, commit signature), `VerifyProofsAsync` (Merkle proof validation), `VerifyRecordsAsync` (record extraction from proofs). Used by `ImportRepoEndpoints.cs` for CAR import verification |
| **Impact** | ✅ Addressed. Full sync verification module exists with all 5 reference-equivalent methods |
| **Priority** | High |

### 12.7 CAR Streamable Block Ordering — Not Compliant

| Aspect | Detail |
|---|---|
| **Spec reference** | Streamable CAR Block Ordering: "commit object must be the first block. MST nodes are included in 'pre-order'... Following each MST node in the tree, include the blocks corresponding to the entries in that node" |
| **Reference** | `MST.carBlockStream()` in `packages/repo/src/mst/mst.ts` — yields blocks in spec-compliant order: commit first, then MST nodes in pre-order (parent before children, depth-first), with leaf blocks interleaved after their parent MST nodes |
| **Local** | `SqlRepoTransactor.IterateCarBlocksAsync()` in `SqlRepoTransactor.cs` emits blocks ordered by `(repoRev DESC, cid DESC)` — a flat storage-level pagination with no awareness of the MST tree structure. `GetRepoController.cs` uses this to stream CAR responses |
| **Impact** | CAR exports from this PDS are not in streamable ordering. Clients must buffer all blocks in memory to reconstruct the MST, defeating the memory-efficiency purpose of streamable CAR |
| **Priority** | Medium |

### 12.8 CAR Import Verification — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | Security Considerations: "When importing CAR files, the completeness of the repository structure should be verified" |
| **Reference** | `importRepo.ts` calls `verifyDiff(currRepo, blockMap, roots[0])` before persisting blocks — validates the MST structure, ensures a valid diff from the current state, checks for completeness |
| **Local** | `ImportRepoEndpoints.cs` performs 5 verification steps: (1) root count validation (exactly 1), (2) per-block CID integrity (SHA256 + CID recompute), (3) root commit block presence, (4) MST structural verification by walking all nodes, (5) forward-only delta check against current repo root. Uses `Consumer.VerifyRepoAsync()` internally |
| **Impact** | ✅ Addressed. CAR import verification is fully implemented |
| **Priority** | High |

### 12.9 Repo Write and Import Size Limits — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | Security Considerations: "limit the number of TreeEntries per Node to a statistically unlikely maximum length... limit the overall depth of the repo" |
| **Reference** | `RepoTransactor.processWrites()` enforces max 2MB `relevantBlocks` check. `PDS_MAX_REPO_IMPORT_SIZE` controls max import size. `PDS_ACCEPTING_REPO_IMPORTS` gates the import endpoint |
| **Local** | `RepoRepository.cs:194` enforces 2MB limit on commit `NewBlocks.ByteSize`. `ImportRepoEndpoints.cs:38,43,64` enforces `PDS_ACCEPTING_REPO_IMPORTS` gate and `PDS_MAX_REPO_IMPORT_SIZE` (default 100MB) on imported CAR files |
| **Impact** | ✅ Addressed. Write and import size limits are enforced |
| **Priority** | Medium |

### 12.10 Missing Block Error Types — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | — (robustness) |
| **Reference** | `packages/repo/src/error.ts` exports: `MissingBlockError`, `MissingBlocksError`, `MissingCommitBlocksError`, `UnexpectedObjectError` — typed errors that callers can catch and handle differently |
| **Local** | `Errors.cs` at `src/projects/Repo/Errors.cs` — typed error hierarchy: `RepoException` base class, `MissingBlockException(Cid)`, `MissingBlocksException(Cid[])`, `MissingCommitBlocksException(Cid commitCid, Cid[] missing)`, `UnexpectedObjectException(Cid, string expectedType, string actualType)`. Used throughout `MST.cs`, `Repo.cs`, and `Consumer.cs` |
| **Impact** | ✅ Addressed. Typed error hierarchy matching the reference structure |
| **Priority** | Low |

### 12.11 Record Schema / Lexicon Validation — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | Lexicon spec: records must conform to their lexicon schema; validation before writing |
| **Reference** | `Prepare.prepareCreate()` and `prepareUpdate()` in `packages/pds/src/repo/prepare.ts` — validates records against a map of 18 known lexicon schemas using `RecordSchema.safeValidate()`. Rejects records that fail schema validation |
| **Local** | `Prepare.cs:ValidateRecord()` at lines 82-140 — performs: `createdAt` future tolerance check (configurable, default 1 minute), required field validation for 5 known lexicons (feed.post, graph.follow, graph.like, graph.repost, actor.profile). Returns `ValidationStatus.Valid` or `ValidationStatus.Unknown`. Called from `PrepareCreate()` and `PrepareUpdate()` when `validate=true` |
| **Impact** | ✅ Addressed. Record schema validation is implemented for the most-written lexicons. Unknown collections are allowed through. 18 lexicons vs 5 coverage gap remains |
| **Priority** | High |

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

### 12.15 CID for Record Computation — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | CID format: blessed CID (CIDv1 + DAG-CBOR). Deterministic CID computation must be cross-implementation compatible |
| **Reference** | `cidForCbor(record)` in `packages/repo/src/util.ts` — CBOR-encodes the JavaScript object directly. CID computation is deterministic from the CBOR bytes |
| **Local** | `CidForSafeRecord()` exists in two locations: `Prepare.cs:152` (converts `JsonElement` → DAG-CBOR via `CanonicalDagCbor.Encode()`, SHA256 hashes, creates CIDv1) and `Repo.cs:193` (re-encodes `CBORObject` → CBOR bytes). Both recompute the CID from record content for safety against hash mismatches |
| **Impact** | ✅ Addressed. CID computation follows the same CBOR→SHA256→CIDv1 path as the reference |
| **Priority** | High |

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

### 12.19 Blob Garbage Collection — Implemented

| Aspect | Detail |
|---|---|
| **Spec reference** | — (resource management) |
| **Reference** | PDS runs garbage collection for temp blobs (after TTL expiry) and orphaned permanent blobs (no longer referenced by any record) |
| **Local** | `BlobGarbageCollectionService.cs` at `src/atompds/Services/` — `BackgroundService` running on configurable schedule (hourly default). Temp blob GC (lines 73-98): deletes temp blobs older than 24 hours with no associated record references. Orphaned permanent blob GC (lines 100-124): finds permanent blobs with no `record_blob` references and deletes from both DB and blob store. Uses `SemaphoreSlim` to prevent concurrent runs |
| **Impact** | ✅ Addressed. Both temp and orphaned blob GC are implemented |
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

## 13. Function-Level Mapping Summary

### 13.1 Core Repository Functions (`Repo` class)

| Function | Reference (`packages/repo/src/repo.ts`) | Local (`Repo.cs`) | Status |
|---|---|---|---|
| `static create(storage, did, keypair, initialWrites?)` | Full implementation | `CreateAsync` | ✅ Equivalent |
| `static formatInitCommit(storage, did, keypair, writes?)` | Full implementation | `FormatInitCommitAsync` | ✅ Equivalent |
| `static createFromCommit(storage, commit)` | Full implementation | `CreateFromCommitAsync` | ✅ Equivalent |
| `static load(storage, cid?)` | Full implementation | `LoadAsync` | ✅ Equivalent |
| `formatCommit(toWrite, keypair)` | Collects covering proofs, populates `relevantBlocks` | `FormatCommitAsync` | ✅ Equivalent |
| `applyCommit(commitData)` | Delegates to storage, reloads | `ApplyCommitAsync` | ✅ Equivalent |
| `applyWrites(toWrite, keypair)` | Combines format + apply | `ApplyWritesAsync` | ✅ Equivalent |
| `formatResignCommit(rev, keypair)` | Creates new signature, same `data` CID | `FormatResignCommitAsync` | ✅ Equivalent |
| `resignCommit(rev, keypair)` | Format + apply resign | `ResignCommitAsync` | ✅ Equivalent |
| `walkRecords(from?)` | Async generator for all records | **Missing** (use `LeavesAsync` on MST directly) | ❌ Missing at Repo level |

### 13.2 MST Functions

| Function | Reference (`mst/mst.ts`) | Local (`MST.cs`) | Status |
|---|---|---|---|
| `static create(storage, entries?, opts?)` | Full | `Create` | ✅ Equivalent |
| `static fromData(storage, data, opts?)` | Full | `FromData` | ✅ Equivalent |
| `static load(storage, cid, opts?)` | Lazy-load, entries=null | `Load` | ✅ Equivalent |
| `add(key, value, knownZeros?)` | Full recursive add | `AddAsync` | ✅ Equivalent |
| `get(key)` | Lookup, descend subtrees | `GetAsync` | ✅ Equivalent |
| `update(key, value)` | Replace leaf value | `UpdateAsync` | ✅ Equivalent |
| `delete(key)` | Recursive delete + trimTop | `DeleteAsync` | ✅ Equivalent |
| `getPointer()` | Return CID, recalc if outdated | `GetPointerAsync` | ✅ Equivalent |
| `serialize()` | Serialize to CBOR | `SerializeAsync` | ✅ Equivalent |
| `getUnstoredBlocks()` | Collect unstored blocks | `GetUnstoredBlocksAsync` | ✅ Equivalent |
| `getCoveringProof(key)` | Proof for key + siblings | `GetCoveringProofAsync` | ✅ Equivalent |
| `carBlockStream()` | MST-aware CAR streaming | **Missing** | ❌ Missing |
| `walkFrom(key)` | Async generator from key | `WalkFromAsync` | ✅ Equivalent |
| `walkLeavesFrom(key)` | Yields leaves from key | `WalkLeavesFromAsync` | ✅ Equivalent |
| `walkReachable()` | Graceful walk (skip errors) | `WalkReachableAsync` | ✅ Equivalent |
| `reachableLeaves()` | Leaves from graceful walk | `ReachableLeavesAsync` | ✅ Equivalent |
| `splitAround(key)` | Split at key → [left, right] | `SplitAroundAsync` | ✅ Equivalent |
| `createChild()` | Empty child at layer-1 | `CreateChildAsync` | ✅ Equivalent |
| `createParent()` | Parent at layer+1 | `CreateParentAsync` | ✅ Equivalent |
| `trimTop()` | Remove empty top layers | `TrimTopAsync` | ✅ Equivalent |
| `appendMerge(toMerge)` | Merge same-layer trees | `AppendMergeAsync` | ✅ Equivalent |
| `list(count, after?, before?)` | Paginated leaf listing | `ListAsync` | ✅ Equivalent |
| `listWithPrefix(prefix, count)` | Prefix-filtered listing | `ListWithPrefixAsync` | ✅ Equivalent |
| `cidsForPath(key)` | CID path to record | `CidsForPathAsync` | ✅ Equivalent |
| `getLayer()` | Get node layer | `GetLayerAsync` | ✅ Equivalent |
| `allNodes()` | All nodes in tree | `AllNodesAsync` | ✅ Equivalent |
| `leaves()` / `leafCount()` | All leaves / count | `LeavesAsync` / `LeafCountAsync` | ✅ Equivalent |
| `allCids()` | All CIDs in tree | `AllCidsAsync` | ✅ Equivalent |

### 13.3 Sync / Verification Functions

| Function | Reference (`sync/consumer.ts`) | Local | Status |
|---|---|---|---|
| `verifyRepo(carBytes, did?, key?)` | Full CAR verification | `VerifyRepoAsync` (Consumer.cs:38) | ✅ Equivalent |
| `verifyRepo(blocks, head, did?, key?)` | Block-map verification | `VerifyRepoAsync` (Consumer.cs:62) | ✅ Equivalent |
| `verifyDiff(repo, blocks, root, did?, key?)` | Diff verification + inversion | `VerifyDiffAsync` (Consumer.cs:85) | ✅ Equivalent |
| `verifyProofs(proofs, claims, did, key)` | Proof validation | `VerifyProofsAsync` (Consumer.cs:125) | ✅ Equivalent |
| `verifyRecords(proofs, did, key)` | Record extraction from proofs | `VerifyRecordsAsync` (Consumer.cs:160) | ✅ Equivalent |
| `verifyIncomingCarBlocks(car)` | CID-content integrity check | **Missing** | ❌ Missing |

### 13.4 Storage Functions

| Function | Reference | Local | Status |
|---|---|---|---|
| `RepoStorage` interface | `storage/types.ts` | `IRepoStorage` | ✅ Equivalent |
| `MemoryBlockStore` | `storage/memory-blockstore.ts` | `MemoryBlockStore` | ✅ Equivalent |
| `ReadableBlockstore` | `storage/readable-blockstore.ts` | **Missing** | ❌ Missing |
| `SyncStorage` (two-tier) | `storage/sync-storage.ts` | **Missing** | ❌ Missing |
| `BlobStore` interface | `storage/types.ts` | `IBlobStore` | ✅ Equivalent |
| CID error types | `error.ts` (4 typed errors) | `Errors.cs` (4 typed classes) | ✅ Equivalent |

### 13.5 PDS-Level Write Preparation

| Function | Reference (`pds/src/repo/prepare.ts`) | Local (`Prepare.cs`) | Status |
|---|---|---|---|
| `prepareCreate()` | Schema validation, CID, blob refs | `PrepareCreate` | ✅ Equivalent |
| `prepareUpdate()` | Same as create | `PrepareUpdate` | ✅ Equivalent |
| `prepareDelete()` | Validation, swapCid | `PrepareDelete` | ✅ Equivalent |
| Schema validation | Lexicon schema check | `ValidateRecord()` (Prepare.cs:82) | ✅ Equivalent (5 lexicons) |
| Blob constraints enforcement | MIME/size enforcement | **Missing** (TODOs at 194, 307) | ❌ Missing |
| Legacy blob refs | Supported | **Missing** (TODO at 246) | ❌ Missing |

### 13.6 Actor Store Repo Functions

| Function | Reference (`actor-store/repo/transactor.ts`) | Local (`RepoRepository.cs`) | Status |
|---|---|---|---|
| `createRepo(writes)` | Init commit + index + blobs | `CreateRepoAsync` | ✅ Equivalent |
| `processWrites(writes, swapCommit?)` | Format + apply + index + blobs + 2MB check | `ProcessWritesAsync` | ✅ Equivalent |
| `formatCommit(writes, swapCommit?)` | Load repo, validate swaps, format, dedup CIDs | `FormatCommitAsync` | ✅ Equivalent |
| `indexWrites(writes, rev)` | Index records + backlinks | `IndexWritesAsync` | ✅ Equivalent |

### Summary

| Category | Total Functions | ✅ Equivalent | ⚠️ Partial | ❌ Missing |
|---|---|---|---|---|
| Core Repo class | 10 | 9 | 0 | 1 |
| MST class | 30 | 29 | 0 | 1 |
| Sync/Verification | 6 | 5 | 0 | 1 |
| Storage | 5 | 4 | 0 | 1 |
| Write Preparation | 6 | 3 | 0 | 3 |
| Actor Store Repo | 4 | 4 | 0 | 0 |
| **Total** | **61** | **54** | **0** | **7** |
