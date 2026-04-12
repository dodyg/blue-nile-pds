# Gap Analysis: blue-nile-pds vs. Canonical ATProto PDS

Comparison against the current reference implementation in [bluesky-social/atproto](https://github.com/bluesky-social/atproto) (`packages/pds`) and the distribution repo at [bluesky-social/pds](https://github.com/bluesky-social/pds).

## Executive Summary

| Metric | blue-nile-pds | Canonical PDS |
|--------|---------------|---------------|
| Native ATProto XRPC endpoints | 26 | 65 |
| Server-level endpoints (`com.atproto.server.*`) | 9 implemented/partial | 25 |
| Identity endpoints (`com.atproto.identity.*`) | 1 | 6 |
| Admin endpoints (`com.atproto.admin.*`) | 0 | 13 |
| Repo endpoints (`com.atproto.repo.*`) | 8 | 10 |
| Sync endpoints (`com.atproto.sync.*`) | 8 | 9 |
| Moderation endpoints (`com.atproto.moderation.*`) | 0 | 1 |
| Temp endpoints (`com.atproto.temp.*`) | 0 | 1 |
| AppView/chat routing | 33 hard-coded methods | generic catchall pipethrough + local handlers |
| OAuth 2.0 | protected-resource only; auth-server stub; DPoP rejected | full provider + resource-server integration |
| Rate limiting | None | configurable method/global limits |
| Real email delivery | Stub only | SMTP via nodemailer |
| Account migration | Local account creation only; DID transfer/import rejected | full import/migration support |
| Read-after-write consistency | None | local viewer / response patching |

**Verdict:** Repo storage, MST/block handling, sequencing, relay sync, and basic legacy JWT session flows are in place. The biggest parity gaps are now OAuth/DPoP auth, generic service pipethrough, account-management endpoints, migration/import, and operational features such as email, rate limiting, admin APIs, and report-service integration.

**Corrections from the previous version of this document:**

- Canonical `com.atproto.server.*` endpoints are **25**, not 26.
- Local `com.atproto.repo.*` coverage is **8**, not 6.
- Canonical `com.atproto.sync.*` coverage is **9**, not 8.
- `com.atproto.moderation.createReport` in the canonical PDS is a **report-service proxy**, not a local moderation database workflow.
- The local App View proxy is **not full pipethrough parity**; it is a fixed whitelist with no `atproto-proxy` support.
- "Image processing" is **not** a major canonical PDS parity target today; the reference PDS has image URL helpers, not an AppView-style thumbnail pipeline.

---

## Priority Definitions

| Priority | Label | Meaning |
|----------|-------|---------|
| **P0** | Critical | Required for broad client interop and core protocol compatibility |
| **P1** | High | Needed for realistic deployment and day-to-day usability |
| **P2** | Medium | Operations, admin, moderation, and migration |
| **P3** | Low | Polish, optional features, and deployment niceties |

**Effort scale:** S = <1 day, M = 1-3 days, L = 3-7 days, XL = 1-2+ weeks

---

## Phase 1: Critical Path (P0)

These gaps block compatibility with the current reference PDS behavior.

### 1.0 Existing Endpoints with Compatibility Gaps

| Endpoint | Gap | Effort |
|----------|-----|--------|
| `com.atproto.server.createAccount` | Rejects imported-account inputs (`did`, `plcOp`) and ignores signup verification inputs (`verificationCode`, `verificationPhone`). | M |
| `com.atproto.server.createSession` | No app-password login path, ignores `allowTakendown`, and cannot accept OAuth/DPoP-based credentials. | M |
| `com.atproto.server.describeServer` | Omits `links`, `contact`, and `phoneVerificationRequired`, so it under-reports server capabilities and policies. | S |
| `com.atproto.server.requestAccountDelete` | Endpoint exists, but production behavior is incomplete because email delivery is still stubbed. | S |

### 1.1 Missing `com.atproto.server.*` Endpoints

| Endpoint | Description | Effort |
|----------|-------------|--------|
| `com.atproto.server.getSession` | Return current session info (DID, handle, email, status) for existing sessions. Clients call this during startup and session restore. | S |
| `com.atproto.server.createAppPassword` | Create an app-specific password. The DB model exists, but login/session issuance still ignores app passwords. | M |
| `com.atproto.server.listAppPasswords` | List app-specific passwords for the authenticated account. | S |
| `com.atproto.server.revokeAppPassword` | Revoke an app-specific password by name and invalidate associated refresh tokens. | S |
| `com.atproto.server.getServiceAuth` | Generate a signed service-auth JWT for inter-service auth. Similar signing logic already exists inside `AppViewProxyController`. | M |
| `com.atproto.server.reserveSigningKey` | Reserve a repo signing key for account creation/migration flows. | M |
| `com.atproto.server.checkAccountStatus` | Report whether an account is ready for migration or reactivation. | S |

### 1.2 Missing `com.atproto.identity.*` Endpoints

| Endpoint | Description | Effort |
|----------|-------------|--------|
| `com.atproto.identity.updateHandle` | Update handle for the authenticated user: validate, update PLC, and keep local state consistent. `HandleManager` already covers handle validation. | M |
| `com.atproto.identity.getRecommendedDidCredentials` | Return recommended DID credentials (rotation key, signing key) for the authenticated user. | S |
| `com.atproto.identity.signPlcOperation` | Sign a PLC directory operation using the server rotation key. `DidLib` signing primitives already exist. | S |
| `com.atproto.identity.submitPlcOperation` | Submit a signed PLC operation to PLC. `PlcClient.SendOperationAsync()` already exists. | S |
| `com.atproto.identity.requestPlcOperationSignature` | Issue an email-backed PLC signature flow for account transfer/update. The `plc_operation` email-token purpose already exists. | M |

### 1.3 Missing `com.atproto.sync.*` Endpoint

| Endpoint | Description | Effort |
|----------|-------------|--------|
| `com.atproto.sync.getLatestCommit` | Return the current repo head CID/rev. The canonical PDS exposes this for sync efficiency; blue-nile-pds does not. | S |

### 1.4 OAuth Resource-Server Compatibility

**Status:** `.well-known/oauth-protected-resource` exists, but authenticated XRPCs still only accept local legacy bearer JWTs. `AuthVerifier` explicitly rejects `DPoP` authorization and has no equivalent to the canonical OAuth middleware / permission-aware verifier.

| Task | Description | Effort |
|------|-------------|--------|
| DPoP-bound access token validation | Accept `DPoP` auth on protected XRPCs and validate the proof, nonce handling, and token binding. | L |
| OAuth issuer / audience verification | Verify OAuth-issued access tokens from the configured auth server or entryway, including audience checks against the target service. | L |
| Permission-aware RPC authorization | Match method permissions the way the canonical pipethrough/auth stack does for proxied methods. | L |

---

## Phase 2: Real-World Deployment (P1)

These gaps block realistic self-hosting and full modern client behavior.

### 2.1 Email Delivery Infrastructure

**Status:** `StubMailer` logs but never sends. `EmailTokenStore` exists and already supports `confirm_email`, `update_email`, `reset_password`, `delete_account`, and `plc_operation`.

| Task | Description | Effort |
|------|-------------|--------|
| Implement `SmtpMailer` | Replace `StubMailer` with a real SMTP implementation (MailKit or similar). Support TLS/STARTTLS and text+HTML templates. | M |
| `com.atproto.server.confirmEmail` | Confirm email ownership using a token from `EmailTokenStore`. | S |
| `com.atproto.server.requestEmailConfirmation` | Send a confirmation email with a token link/code. | S |
| `com.atproto.server.requestEmailUpdate` | Send an email update token to the new address. | S |
| `com.atproto.server.updateEmail` | Update account email after token verification. | S |
| `com.atproto.server.requestPasswordReset` | Send a password reset token via email. | S |
| `com.atproto.server.resetPassword` | Reset password using a valid email token. | S |
| `com.atproto.server.createInviteCode` | Create a single invite code. Invite DB primitives already exist. | S |
| `com.atproto.server.createInviteCodes` | Create multiple invite codes in one call. | S |
| `com.atproto.server.getAccountInviteCodes` | List invite codes created by the authenticated account. | S |

### 2.2 Rate Limiting

**Status:** Completely absent. The canonical PDS applies method-level rate limits and exposes configurable global rate-limit settings in environment config; blue-nile-pds defines no equivalent middleware.

| Task | Description | Effort |
|------|-------------|--------|
| Per-IP rate limiting | Global rate limit using ASP.NET Core rate-limiter middleware. | M |
| Per-method auth-sensitive limits | Add stricter limits for login, email confirmation, password reset, and account management endpoints. | M |
| Per-user repo write limits | Add write budgeting for create/put/delete/applyWrites. | M |
| Rate-limit headers and errors | Return `Retry-After` / `RateLimit-*` style signals and consistent 429s. | S |
| Configuration | Add `PDS_RATE_LIMITS_ENABLED`-style toggles and thresholds. | S |

### 2.3 OAuth 2.0 Provider / Entryway Integration

**Status:** `.well-known/oauth-protected-resource` is populated. `.well-known/oauth-authorization-server` returns a TODO stub. There is no token endpoint, authorization endpoint, PKCE, client registration, or entryway/trusted-client integration.

| Task | Description | Effort |
|------|-------------|--------|
| Authorization server metadata | Replace the TODO stub with real metadata. | S |
| Authorization endpoint | Implement consent and auth-code issuance. | L |
| Token endpoint | Implement code exchange and refresh issuance. | L |
| PKCE support | Implement S256 PKCE challenge/verification. | S |
| DPoP nonce/proof support | Complete the provider-side DPoP flow, not just resource-server validation. | M |
| OAuth session store | Track OAuth sessions, grants, and refresh chains. | L |
| Trusted clients / entryway | Support canonical trusted-client configuration and entryway-aware flows. | M |

### 2.4 Service Pipethrough / AppView & Chat Parity

**Status:** `AppViewProxyController` exposes a fixed list of 33 `app.bsky.*` / `chat.bsky.*` methods. The canonical PDS has a generic service-aware pipethrough layer with `atproto-proxy` support, protected-method exclusions, service-DID resolution, report/mod-service routing, and broader chat coverage.

| Task | Description | Effort |
|------|-------------|--------|
| Generic catchall pipethrough | Replace the fixed whitelist with a service-aware catchall similar to canonical `pipethrough.ts`. | L |
| `atproto-proxy` header support | Parse and validate explicit proxy targets by DID + service ID. | M |
| Report/mod-service routing | Route `com.atproto.moderation.createReport` and moderation/admin surfaces to the configured service when appropriate. | M |
| Protected-method exclusions | Prevent account-management methods from being proxied/service-authed incorrectly. | S |
| Chat parity | Add missing `chat.bsky.*` coverage beyond the current two chat handlers. | M |

### 2.5 Signup Anti-Abuse & Server Metadata

**Status:** `createAccount` ignores `verificationCode` / `verificationPhone`, and `describeServer` returns only DID, domains, and invite requirement. The canonical PDS has config for hCaptcha, contact/policy URLs, and phone-verification signaling.

| Task | Description | Effort |
|------|-------------|--------|
| hCaptcha / signup verification | Add optional verification for account creation. | M |
| Phone verification signaling | Expose `phoneVerificationRequired` when configured. | S |
| Policy/contact metadata | Populate `describeServer.links` and `describeServer.contact`. | S |
| Entryway-aware signup behavior | Match canonical account-creation behavior when running behind entryway/trusted auth flows. | M |

### 2.6 Read-After-Write Consistency

**Status:** Completely absent. Local writes only appear in App View responses after upstream indexing catches up. The canonical PDS has a dedicated read-after-write viewer and pipethrough patching helpers.

| Task | Description | Effort |
|------|-------------|--------|
| Write snapshot cache | Cache recent writes per DID with TTL or cursor awareness. | M |
| Proxy response patching | Merge recent local writes into proxied App View responses. | L |
| Catch-up invalidation | Expire/clear patches once the upstream service has caught up. | M |

---

## Phase 3: Operations & Administration (P2)

### 3.1 Missing `com.atproto.admin.*` Endpoints

**Status:** None implemented. The local `pdsadmin` tool already calls several of these endpoints, but the server does not expose them.

| Endpoint | Description | Effort |
|----------|-------------|--------|
| `com.atproto.admin.getAccountInfo` | Get detailed account info by DID or handle. | S |
| `com.atproto.admin.getAccountInfos` | Batch get account info. | S |
| `com.atproto.admin.deleteAccount` | Admin-forced account deletion. | M |
| `com.atproto.admin.updateAccountHandle` | Admin-forced handle update. | M |
| `com.atproto.admin.updateAccountEmail` | Admin-forced email update. | S |
| `com.atproto.admin.updateAccountPassword` | Admin-forced password reset. | S |
| `com.atproto.admin.getSubjectStatus` | Get moderation/takedown status for a subject. | S |
| `com.atproto.admin.updateSubjectStatus` | Apply or remove takedown on an account/repo. | M |
| `com.atproto.admin.enableAccountInvites` | Re-enable invite creation for an account. | S |
| `com.atproto.admin.disableAccountInvites` | Disable invite creation for an account. | S |
| `com.atproto.admin.disableInviteCodes` | Disable specific invite codes. | S |
| `com.atproto.admin.getInviteCodes` | List invite codes with filtering. | S |
| `com.atproto.admin.sendEmail` | Send an email to an account via the admin API. | S |

### 3.2 Moderation / Report-Service Integration

**Status:** `com.atproto.moderation.createReport` is entirely absent. In the canonical PDS this is forwarded to a configured report service with service auth; it is not a local moderation DB feature.

| Task | Description | Effort |
|------|-------------|--------|
| `com.atproto.moderation.createReport` | Accept user reports and forward them to the configured report service. | M |
| Report-service config and DID wiring | Add `PDS_REPORT_SERVICE_URL` / DID-style config and service-auth plumbing. | M |
| Error propagation and observability | Surface upstream report-service failures cleanly instead of falling back to opaque 500s. | S |

### 3.3 Account Migration / Repo Import

**Status:** `CreateAccountController` explicitly rejects imported-account inputs (`did`, `plcOp`). There is no `importRepo` endpoint or portability flow. The local `src/migration/` project is a storage migration tool, not ATProto account migration.

| Task | Description | Effort |
|------|-------------|--------|
| `com.atproto.repo.importRepo` | Import a full repo from a CAR file. | XL |
| Imported-account creation | Accept `did` / `plcOp` during account creation. | L |
| Migration validation | Validate imported repos and DID/handle alignment. | L |
| Account portability flows | Coordinate source deactivation and destination activation. | L |

### 3.4 Background Job Processing

**Status:** No background queue exists. The canonical PDS has a simple in-process `p-queue` background worker used for out-of-band work.

| Task | Description | Effort |
|------|-------------|--------|
| Job queue infrastructure | Add a background worker abstraction for non-request-critical work. | M |
| Async email delivery | Move email sending off the request path. | S |
| Async relay / crawler notification | Move best-effort external notifications off the request path. | S |

### 3.5 Missing `com.atproto.repo.*` Endpoint

| Endpoint | Description | Effort |
|----------|-------------|--------|
| `com.atproto.repo.listMissingBlobs` | List blobs referenced in records but missing from the blob store. | M |

### 3.6 Admin CLI / Server Mismatch

**Status:** `src/pdsadmin/Program.cs` assumes server support for `com.atproto.server.createInviteCode`, `com.atproto.admin.getAccountInfo`, `com.atproto.admin.updateSubjectStatus`, and `com.atproto.admin.updateAccountPassword`, but those endpoints do not exist locally.

| Task | Description | Effort |
|------|-------------|--------|
| Align `pdsadmin` with actual server capabilities | Either implement the missing APIs or retarget the CLI to local-only admin surfaces. | M |
| Add capability checks | Fail fast with useful errors when the remote PDS does not expose required admin methods. | S |

---

## Phase 4: Polish & Optional Features (P3)

### 4.1 Redis Integration

**Status:** No Redis support. The canonical PDS uses Redis scratch storage for optional distributed functionality such as rate-limiting support.

| Task | Description | Effort |
|------|-------------|--------|
| Redis-backed rate-limit counters | Use Redis when running more than one instance. | M |
| Optional scratch/cache integration | Add a Redis scratch service similar to the canonical config surface. | M |
| Configuration | Add `PDS_REDIS_*`-style environment support. | S |

### 4.2 Push Notifications

| Endpoint | Description | Effort |
|----------|-------------|--------|
| `app.bsky.notification.registerPush` | The canonical PDS has a handler; blue-nile-pds does not. | M |

### 4.3 Temp Endpoint

| Endpoint | Description | Effort |
|----------|-------------|--------|
| `com.atproto.temp.checkSignupQueue` | Check signup queue status. Mostly relevant for larger hosted deployments. | S |

### 4.4 TLS / Domain Verification

**Status:** The distribution repo exposes `GET /tls-check` for Caddy on-demand TLS handle verification. blue-nile-pds has no equivalent route.

| Task | Description | Effort |
|------|-------------|--------|
| `GET /tls-check` | Verify hosted-handle/domain ownership for deployment tooling. | S |

### 4.5 Basic Host Routes

**Status:** The canonical PDS serves a root landing page and `robots.txt`. blue-nile-pds only exposes `_health` and `.well-known/*`.

| Task | Description | Effort |
|------|-------------|--------|
| `GET /` | Serve a basic service landing/info page. | S |
| `GET /robots.txt` | Match canonical crawler behavior. | S |

---

## Feature Comparison Matrix

### Infrastructure

| Feature | blue-nile-pds | Canonical PDS | Gap |
|---------|---------------|---------------|-----|
| Repo CRUD (MST + blocks) | ✅ Full | ✅ Full | None |
| Sync firehose (`subscribeRepos`) | ✅ Full | ✅ Full | None |
| Blob storage (disk) | ✅ Full | ✅ Full | None |
| Blob storage (S3) | ✅ Full | ✅ Full | None |
| Account DB + migrations | ✅ EF Core | ✅ Kysely/SQLite | None (different ORM) |
| Per-actor stores | ✅ Full | ✅ Full | None |
| Sequencer / outbox | ✅ Full | ✅ Full | None |
| DID resolution + caching | ✅ Full | ✅ Full | None |
| Handle validation | ✅ Full | ✅ Full | None |
| Disposable email filtering | ✅ Full | ✅ Full | None |
| Legacy JWT auth (access + refresh) | ✅ Full | ✅ Full | None |
| Server account-management surface | ⚠️ Partial | ✅ Full | Missing and partially compatible server endpoints |
| Identity / PLC account-management flows | ⚠️ Partial | ✅ Full | Missing update/sign/submit/request flows |
| OAuth resource-server auth (DPoP, permissions) | ❌ None | ✅ Full | **Full gap** |
| OAuth provider / entryway integration | ⚠️ Stub | ✅ Full | **Major gap** |
| AppView/chat pipethrough | ⚠️ Fixed whitelist only | ✅ Catchall + service-aware | **Major gap** |
| Report-service moderation routing | ❌ None | ✅ Full | **Full gap** |
| Real email delivery | ❌ Stub only | ✅ SMTP | **Full gap** |
| Rate limiting | ❌ None | ✅ Configurable | **Full gap** |
| Signup anti-abuse / phone verification | ❌ None | ✅ Optional/full | **Gap** |
| Read-after-write consistency | ❌ None | ✅ Full | **Full gap** |
| Background job queue | ❌ None | ✅ `p-queue` | **Full gap** |
| Account migration / repo import | ❌ None | ✅ Full | **Full gap** |
| Redis scratch support | ❌ None | ✅ Optional | **Full gap** |
| Basic service metadata / policy links | ⚠️ Partial | ✅ Full | **Gap** |
| Basic host routes (`/`, `robots.txt`, `/tls-check`) | ⚠️ `_health` + `.well-known/*` only | ✅ Full | **Gap** |

### XRPC Endpoints

Legend: ✅ Implemented and broadly compatible, ⚠️ Partial/incomplete, ❌ Missing

#### `com.atproto.server.*`

| Endpoint | Status | Priority |
|----------|--------|----------|
| `createAccount` | ⚠️ | P0 |
| `createSession` | ⚠️ | P0 |
| `deleteSession` | ✅ | — |
| `refreshSession` | ✅ | — |
| `describeServer` | ⚠️ | P1 |
| `activateAccount` | ✅ | — |
| `deactivateAccount` | ✅ | — |
| `requestAccountDelete` | ⚠️ | P1 |
| `deleteAccount` | ✅ | — |
| `getSession` | ❌ | P0 |
| `createAppPassword` | ⚠️ | P0 |
| `listAppPasswords` | ⚠️ | P0 |
| `revokeAppPassword` | ⚠️ | P0 |
| `getServiceAuth` | ⚠️ | P0 |
| `reserveSigningKey` | ❌ | P0 |
| `checkAccountStatus` | ❌ | P0 |
| `confirmEmail` | ⚠️ | P1 |
| `requestEmailConfirmation` | ⚠️ | P1 |
| `requestEmailUpdate` | ⚠️ | P1 |
| `updateEmail` | ⚠️ | P1 |
| `requestPasswordReset` | ⚠️ | P1 |
| `resetPassword` | ⚠️ | P1 |
| `createInviteCode` | ⚠️ | P1 |
| `createInviteCodes` | ⚠️ | P1 |
| `getAccountInviteCodes` | ⚠️ | P1 |

#### `com.atproto.identity.*`

| Endpoint | Status | Priority |
|----------|--------|----------|
| `resolveHandle` | ✅ | — |
| `updateHandle` | ⚠️ | P0 |
| `getRecommendedDidCredentials` | ⚠️ | P0 |
| `signPlcOperation` | ⚠️ | P0 |
| `submitPlcOperation` | ⚠️ | P0 |
| `requestPlcOperationSignature` | ⚠️ | P0 |

#### `com.atproto.repo.*`

| Endpoint | Status | Priority |
|----------|--------|----------|
| `createRecord` | ✅ | — |
| `putRecord` | ✅ | — |
| `deleteRecord` | ✅ | — |
| `applyWrites` | ✅ | — |
| `getRecord` | ✅ | — |
| `listRecords` | ✅ | — |
| `uploadBlob` | ✅ | — |
| `describeRepo` | ✅ | — |
| `importRepo` | ❌ | P2 |
| `listMissingBlobs` | ❌ | P2 |

#### `com.atproto.sync.*`

| Endpoint | Status | Priority |
|----------|--------|----------|
| `getBlob` | ✅ | — |
| `getBlocks` | ✅ | — |
| `getRecord` | ✅ | — |
| `getRepo` | ✅ | — |
| `getRepoStatus` | ✅ | — |
| `listBlobs` | ✅ | — |
| `listRepos` | ✅ | — |
| `subscribeRepos` | ✅ | — |
| `getLatestCommit` | ❌ | P0 |

#### `com.atproto.admin.*`

| Endpoint | Status | Priority |
|----------|--------|----------|
| `getAccountInfo` | ❌ | P2 |
| `getAccountInfos` | ❌ | P2 |
| `deleteAccount` | ❌ | P2 |
| `updateAccountHandle` | ❌ | P2 |
| `updateAccountEmail` | ❌ | P2 |
| `updateAccountPassword` | ❌ | P2 |
| `getSubjectStatus` | ❌ | P2 |
| `updateSubjectStatus` | ❌ | P2 |
| `enableAccountInvites` | ❌ | P2 |
| `disableAccountInvites` | ❌ | P2 |
| `disableInviteCodes` | ❌ | P2 |
| `getInviteCodes` | ❌ | P2 |
| `sendEmail` | ❌ | P2 |

#### `com.atproto.moderation.*`

| Endpoint | Status | Priority |
|----------|--------|----------|
| `createReport` | ❌ | P2 |

#### `com.atproto.temp.*`

| Endpoint | Status | Priority |
|----------|--------|----------|
| `checkSignupQueue` | ❌ | P3 |

### AppView / Chat / Service Routing

| Surface | Status | Priority |
|---------|--------|----------|
| Hard-coded `app.bsky.*` / `chat.bsky.*` whitelist | ⚠️ | — |
| Generic catchall pipethrough | ❌ | P1 |
| `atproto-proxy` header support | ❌ | P1 |
| Broad `chat.bsky.*` coverage | ❌ | P1 |
| Report/mod-service service routing | ❌ | P2 |
| `app.bsky.notification.registerPush` | ❌ | P3 |

---

## Recommended Implementation Order

### Sprint 1 — Client Interop (P0)

Goal: match the minimum compatibility surface expected by current ATProto clients and services.

1. OAuth resource-server auth (`DPoP`, issuer/audience verification, permission-aware authz)
2. `getSession`
3. App-password trio: `createAppPassword` / `listAppPasswords` / `revokeAppPassword`
4. Wire app-password login/session issuance into `createSession`
5. `updateHandle`
6. `getRecommendedDidCredentials`
7. `signPlcOperation` / `submitPlcOperation` / `requestPlcOperationSignature`
8. `getServiceAuth`
9. `createAccount` parity for imported-account inputs (`did`, `plcOp`)
10. `checkAccountStatus`
11. `getLatestCommit`

**Estimated total: ~3-4 weeks**

### Sprint 2 — Real-World Deployment (P1)

Goal: make the PDS usable for real users and modern clients.

1. Implement `SmtpMailer`
2. Complete email confirmation / update / reset flows
3. Add rate limiting
4. Complete OAuth provider / entryway integration
5. Replace the fixed App View proxy with generic service-aware pipethrough
6. Add signup anti-abuse and `describeServer` metadata parity
7. Add read-after-write consistency

**Estimated total: ~6-8 weeks**

### Sprint 3 — Operations & Admin (P2)

Goal: make the PDS operable and support migration/reporting workflows.

1. Admin endpoints
2. `com.atproto.moderation.createReport` + report-service integration
3. Background job queue
4. `importRepo` and migration/account portability work
5. `listMissingBlobs`
6. Align or replace `pdsadmin`

**Estimated total: ~4-6 weeks**

### Sprint 4 — Polish (P3)

Goal: close remaining low-priority parity gaps.

1. Redis scratch integration
2. `app.bsky.notification.registerPush`
3. `com.atproto.temp.checkSignupQueue`
4. `GET /tls-check`
5. Basic host routes (`/`, `robots.txt`)

**Estimated total: ~2-3 weeks**

---

## Notes

- The canonical PDS is **TypeScript/Node.js** with **Express**, **Kysely**, and a dedicated OAuth/auth stack. This repo is **C#/.NET 10** with **ASP.NET Core** and **EF Core**. The target is behavioral parity, not architectural parity.
- The canonical PDS currently exposes **65 native ATProto XRPC endpoints**: 25 server, 6 identity, 13 admin, 10 repo, 9 sync, 1 moderation, 1 temp.
- The local App View proxy should be considered **partial** even though many high-traffic endpoints are present. The real gap is not just "missing more proxied methods"; it is the absence of the canonical **service-aware pipethrough model**.
- The canonical `com.atproto.moderation.createReport` route is a **service-auth proxy to a report service**, so parity does **not** require building local moderation tables first.
- The local code already contains useful building blocks for parity work: `InviteStore`, `EmailTokenStore`, `DidLib` PLC signing primitives, `PlcClient`, app-password DB tables, and service-JWT generation logic inside `AppViewProxyController`.
- The local `pdsadmin` project currently assumes server/admin APIs that do not exist in this server. That mismatch is itself an operational gap worth tracking.
