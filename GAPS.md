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

### 1.2 Local Endpoints Fully Implemented

All non-deprecated `com.atproto.*` endpoints are present:
- `admin.*` (13 endpoints)
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

**Local:** The catch-all proxy only matches `app.bsky.*`, `chat.bsky.*`, and `com.atproto.moderation.*`. No `tools.ozone.*` routes are proxied. No `PDS_MOD_SERVICE_URL` or `PDS_MOD_SERVICE_DID` configuration exists.

### 2.2 Report Service Routing — Missing

The canonical PDS routes `com.atproto.moderation.createReport` to a dedicated report service (`PDS_REPORT_SERVICE_URL`, defaults to `https://mod.bsky.app`). The local implementation handles `createReport` locally rather than proxying it. Configuration `PDS_REPORT_SERVICE_URL` and `PDS_REPORT_SERVICE_DID` exist in `ServerEnvironment.cs` but are never used for routing `createReport` to an external service.

### 2.3 Chat Service Proxying — Incomplete

The canonical PDS proxies **14 `chat.bsky.*` methods** to a chat service via the `PRIVILEGED_METHODS` set:

| Method | Local Status |
|---|---|
| `chat.bsky.actor.deleteAccount` | **Stub** (returns OK without proxying) |
| `chat.bsky.actor.exportAccountData` | **Missing** |
| `chat.bsky.convo.deleteMessageForSelf` | **Missing** |
| `chat.bsky.convo.getConvo` | **Missing** |
| `chat.bsky.convo.getConvoForMembers` | **Missing** |
| `chat.bsky.convo.getLog` | **Missing** |
| `chat.bsky.convo.getMessages` | **Missing** |
| `chat.bsky.convo.leaveConvo` | **Missing** |
| `chat.bsky.convo.listConvos` | **Proxied** (via static route) |
| `chat.bsky.convo.muteConvo` | **Missing** |
| `chat.bsky.convo.sendMessage` | **Missing** |
| `chat.bsky.convo.sendMessageBatch` | **Missing** |
| `chat.bsky.convo.unmuteConvo` | **Missing** |
| `chat.bsky.convo.updateRead` | **Missing** |

### 2.4 Protected Method Enforcement — Missing

The canonical PDS defines 16 `PROTECTED_METHODS` that must never be proxied (session, email, identity, app password operations). The local proxy has no such enforcement — the catch-all could theoretically proxy methods that should only be handled locally.

### 2.5 Privileged Method Enforcement — Missing

The canonical PDS marks chat methods and `createAccount` as `PRIVILEGED_METHODS` requiring `AppPassPrivileged` scope when proxied. The local implementation has no equivalent scope gating on proxied requests.

### 2.6 CDN URL Pattern — Stored but Not Used

`PDS_BSKY_APP_VIEW_CDN_URL_PATTERN` is defined in config and stored in `BskyAppViewConfig`, but is never consumed at runtime. The canonical PDS uses it to rewrite blob/image URLs to CDN endpoints for better performance.

### 2.7 Response Header Forwarding — Incomplete

The canonical proxy forwards these response headers from upstream services:
- `atproto-repo-rev`
- `atproto-content-labelers`
- `retry-after`

The local proxy does not forward these headers.

### 2.8 Content Encoding Negotiation — Missing

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

### 4.1 Admin Password — Hardcoded

The canonical PDS reads `PDS_ADMIN_PASSWORD` from environment. The local implementation **hardcodes the admin password to `"secret"`** in `ServerConfig.cs:314`. This is a security issue.

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
| `PDS_ADMIN_PASSWORD` | (required) | Admin API password | **Missing** (hardcoded "secret") |
| `PDS_DPOP_SECRET` | (none) | DPoP proof secret | **Missing** |
| `PDS_RATE_LIMIT_BYPASS_KEY` | (none) | Bypass key for rate limits | **Missing** |
| `PDS_RATE_LIMIT_BYPASS_IPS` | (none) | Bypass IPs for rate limits | **Missing** |
| `PDS_REDIS_SCRATCH_PASSWORD` | (none) | Redis password | **Missing** (has `PDS_REDIS_URL` only) |
| `PDS_SQLITE_DISABLE_WAL_AUTO_CHECKPOINT` | `false` | Disable WAL auto-checkpoint | **Missing** (code exists, no env var) |
| `PDS_HANDLE_BACKUP_NAMESERVERS` | (none) | Backup DNS nameservers | **Missing** |
| `PDS_MODERATION_EMAIL_SMTP_URL` | (none) | Separate moderation SMTP | **Missing** |
| `PDS_MODERATION_EMAIL_ADDRESS` | (none) | Moderation sender address | **Missing** |
| `PDS_ACCEPTING_REPO_IMPORTS` | (none) | Allow account imports | **Missing** |
| `PDS_MAX_REPO_IMPORT_SIZE` | (none) | Max import size | **Missing** |
| `PDS_HCAPTCHA_TOKEN_SALT` | (none) | hCaptcha token salt | **Missing** |
| `PDS_ENTRYWAY_PLC_ROTATION_KEY` | (none) | Entryway PLC key | **Missing** |
| `PDS_ENTRYWAY_ADMIN_TOKEN` | (none) | Entryway admin token | **Missing** |
| `PDS_MOD_SERVICE_URL` | (none) | Ozone mod service URL | **Missing** |
| `PDS_MOD_SERVICE_DID` | (none) | Ozone mod service DID | **Missing** |
| `PDS_LEXICON_AUTHORITY_DID` | (none) | Lexicon authority DID | **Missing** |
| `LOG_LEVEL` | `info` | PDS-specific log level | **Missing** |
| `LOG_DESTINATION` | (stderr) | PDS-specific log destination | **Missing** |
| `PDS_PRIMARY_COLOR` (+ contrast, hue) | (none) | OAuth branding | **Missing** |
| `PDS_ERROR_COLOR`, `WARNING_COLOR`, etc. | (none) | OAuth theme colors | **Missing** |
| `PDS_CONTRAST_SATURATION` | (none) | Branding saturation | **Missing** |

### 5.2 Config Structural Differences

| Aspect | Canonical | Local |
|---|---|---|
| SMTP config | Single `PDS_EMAIL_SMTP_URL` (supports smtp://, smtps://, sendmail://) | Separate `PDS_SMTP_HOST/PORT/USERNAME/PASSWORD/USE_TLS` |
| DID cache TTL defaults | `staleTTL=1hr`, `maxTTL=1day` | `staleTTL=1day`, `maxTTL=1hr` (swapped!) |
| Invite required default | `true` | `false` |
| Blob upload limit naming | `PDS_BLOB_UPLOAD_LIMIT` | `PDS_BLOB_UPLOAD_LIMIT_IN_BYTES` |

**Note:** The DID cache TTL values appear to be **swapped** in the local implementation — `PDS_DID_CACHE_STALE_TTL` defaults to 86400000ms (1 day) and `PDS_DID_CACHE_MAX_TTL` defaults to 3600000ms (1 hour), whereas the canonical has stale=1hr and max=1day. This may cause different caching behavior.

---

## 6. Services & Infrastructure Gaps

### 6.1 DID Cache — In-Memory vs. Persistent

The canonical PDS uses a separate SQLite database (`did_cache.sqlite`) for DID resolution caching with stale-while-revalidate semantics. The local implementation uses an in-memory `ConcurrentDictionary` (`MemoryCache` in `src/projects/Identity/`). This means:
- Cache is lost on restart
- No persistence across instances
- Memory grows unbounded (within TTL constraints)

### 6.2 SSRF Protection — Config Only

The canonical PDS implements actual SSRF protection via undici's `Agent` with unicast IP checking — all outgoing HTTP requests (proxy, fetch) are filtered to prevent connections to internal/private IP addresses.

The local implementation:
- Has `PDS_DISABLE_SSRF_PROTECTION` config flag
- Stores it in `ProxyConfig.DisableSsrfProtection`
- **Never reads or enforces it** — no actual IP filtering logic exists

### 6.3 Recovery / Maintenance Scripts — Missing

The canonical PDS includes several critical maintenance scripts:

| Script | Purpose |
|---|---|
| `rebuild-repo` | Rebuild a repo's MST from record table data |
| `publish-identity` | Publish identity events on firehose |
| `rotate-keys` | Ensure PLC signing key matches local key |
| `sequencer-recovery` | Replay sequencer for full data recovery |
| `recovery-repair-repos` | Rebuild repos that failed during recovery |

The local implementation has **none** of these. Only the migration utility exists.

### 6.4 Separate Moderation Mailer — Missing

The canonical PDS has a separate `ModerationMailer` with its own SMTP configuration (`PDS_MODERATION_EMAIL_SMTP_URL`). This allows moderation emails to be sent through a different email infrastructure. The local implementation has a single mailer.

### 6.5 Image Processing — Missing

The canonical PDS has an `image/` module with `ImageUrlBuilder` for constructing CDN-optimized image URLs (thumbnail generation, format conversion). The local implementation has no image processing capabilities.

### 6.6 Handle Backup Nameservers — Missing

The canonical PDS supports `PDS_HANDLE_BACKUP_NAMESERVERS` for fallback DNS resolution during handle verification. This improves reliability when primary DNS is unavailable. The local implementation has no backup nameserver support.

### 6.7 Read-After-Write — Partial

The canonical PDS has a full `read-after-write/` module that:
- Intercepts proxied responses
- Buffers the response for local inspection
- Patches records/collections that were recently written
- Handles content encoding during patching
- Supports a `LocalViewer` for viewing records without proxying

The local implementation has `WriteSnapshotCache` which tracks recent writes and patches proxied responses, but:
- Only patches specific fields (`displayName`, `description`, `name`)
- Doesn't handle content encoding
- Doesn't buffer/proxy responses for inspection

### 6.8 Account Device Tracking — Missing

The canonical PDS tracks devices (`device`, `account_device` tables) for security auditing and session management. The local implementation has these tables commented out in the account manager migration.

### 6.9 Used Refresh Token Tracking — Missing

The canonical PDS tracks used refresh tokens in a `used_refresh_token` table to detect token replay attacks. The local implementation has this table commented out.

### 6.10 Lexicon Resolver — Missing

The canonical PDS has a `LexiconResolver` for dynamically resolving lexicon definitions at runtime. This is used for OAuth scope resolution and validation. The local implementation has no lexicon resolver.

### 6.11 Scope Reference Getter — Missing

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

| Area | Canonical Tests | Local Tests |
|---|---|---|
| Account management | `account.test.ts`, `account-deactivation.test.ts`, `account-deletion.test.ts`, `account-migration.test.ts` | **None** |
| Authentication | `auth.test.ts`, `moderator-auth.test.ts` | **None** |
| App passwords | `app-passwords.test.ts` | **None** |
| Blob operations | `blob-deletes.test.ts`, `file-uploads.test.ts` | **None** |
| CRUD operations | `crud.test.ts`, `create-post.test.ts` | **None** |
| Email | `email-confirmation.test.ts` | **None** |
| Entryway | `entryway.test.ts` | **None** |
| Handles | `handles.test.ts`, `handle-validation.test.ts` | **None** |
| Invite codes | `invite-codes.test.ts`, `invites-admin.test.ts` | **None** |
| Moderation | `moderation.test.ts` | **None** |
| OAuth | `oauth.test.ts` | **None** |
| PLC operations | `plc-operations.test.ts` | **None** |
| Preferences | `preferences.test.ts` | **None** |
| Race conditions | `races.test.ts` | **None** |
| Rate limiting | `rate-limits.test.ts` | **None** |
| Recovery | `recovery.test.ts` | **None** |
| Sequencer | `sequencer.test.ts` | **None** |
| Server | `server.test.ts` | **None** |
| Takedown | `takedown-appeal.test.ts` | **None** |
| Database | `db.test.ts` | **None** |
| Proxy views | 8 proxied test files | **None** |
| Sync | 4 sync test files | **None** |
| CID | — | 12 tests |
| Common (TID, CBOR) | — | 10 tests |
| ActorStore blob refs | — | 156 tests |

**Total canonical:** ~40+ test files with comprehensive integration tests. **Local:** 3 test projects with 178 tests covering only low-level utilities.

---

## 10. Deployment Gaps

### 10.1 Container / Docker Support — Missing

The canonical PDS provides:
- `Dockerfile` with multi-stage build
- `compose.yaml` with Caddy reverse proxy + automatic TLS
- `installer.sh` for interactive VPS setup
- ACME/Let's Encrypt certificate automation

The local implementation has none of these.

### 10.2 Graceful Shutdown — Missing

The canonical PDS implements graceful shutdown via SIGTERM handling with queue draining. The local `BackgroundJobWorker` does not implement `IHostedService.StopAsync` with explicit drain logic (relies on ASP.NET Core's default `BackgroundService` cancellation).

---

## 11. Additional Minor Gaps

### 11.1 Health Check — Simplified

The canonical `_health` endpoint executes `SELECT 1` against the account database to verify connectivity. The local health endpoint exists but may not perform the same database connectivity check.

### 11.2 Landing Page — Different

The canonical PDS serves ASCII art at `/` with links to code and documentation. The local implementation returns a JSON service info object.

### 11.3 robots.txt — Different

The canonical PDS allows all crawling (`Allow: /`). The local implementation restricts to `Allow: /xrpc/` and `Disallow: /`.

### 11.4 Import Repo — No Size Limit

The canonical PDS has `PDS_MAX_REPO_IMPORT_SIZE` and `PDS_ACCEPTING_REPO_IMPORTS` to control repo imports. The local `ImportRepoController` reads the entire body into memory with no size limit.

### 11.5 Logging — Standard vs. Custom

The canonical PDS uses Pino with configurable `LOG_LEVEL` and `LOG_DESTINATION`. The local implementation uses standard ASP.NET Core logging with `appsettings.json` defaults.

### 11.6 `PDS_SERVICE_DID` Default

The canonical PDS defaults `PDS_SERVICE_DID` to `did:web:{hostname}`. The local implementation requires explicit configuration or falls back to constructing it from the hostname.

### 11.7 Email Templates

The canonical PDS uses Handlebars templates for emails (`confirm-email.hbs`, `delete-account.hbs`, `plc-operation.hbs`, `reset-password.hbs`, `update-email.hbs`). The local `SmtpMailer` sends plain-text emails only.

---

## Summary Statistics

| Category | Canonical | Local | Gap |
|---|---|---|---|
| XRPC endpoints (non-deprecated) | ~65 | ~65 | Minimal |
| Deprecated endpoints | 2 | 0 | 2 missing |
| Proxy targets (ozone/chat/report) | Full | Partial | Significant |
| OAuth features | Full provider | Basic PKCE | Major |
| Auth scopes/methods | 10+ modes | 5 modes | Significant |
| Config variables | ~90+ | ~70 | ~20 missing |
| Account DB tables | 15 | 8 | 7 missing/commented |
| Test files | ~40+ | 3 | ~37 missing |
| Recovery scripts | 6 | 0 | All missing |
| Deployment tooling | Docker+Caddy | None | All missing |
