# Implementation Plan: blue-nile-pds Parity

> **Companion to:** `GAP.md` — read that first for the full gap analysis and feature comparison matrix.
> **Audience:** An agent session picking up implementation work. This document tells you *what to build, in what order, and why*, with concrete pointers into the codebase.

---

## How to Use This Document

1. Read `GAP.md` for the complete parity analysis.
2. Work through tiers sequentially. Within each tier, items are ordered by dependency and impact.
3. Each item links to existing code you can build on.
4. After completing an item, run `dotnet build atompds.slnx` and relevant tests before moving on.
5. Mark items as done by updating the status in the tables below.

---

## Existing Building Blocks

These already exist in the codebase and are referenced throughout the plan:

| Building Block | Location | Notes |
|---|---|---|
| `AppPassword` DB model + `AppPasswords` DbSet | `src/pds_projects/AccountManager/Db/Models.cs:116-125` | Table `app_password` with `{did, name}` PK, `PasswordSCrypt`, `Privileged` |
| `RefreshToken.AppPasswordName` | `src/pds_projects/AccountManager/` | Refresh tokens already carry optional app-password name |
| `Auth.RevokeAppPasswordRefreshTokenAsync()` | Auth service | Revokes refresh tokens tied to an app password |
| `PasswordStore` (SCrypt) | `src/pds_projects/AccountManager/Db/PasswordStore.cs` | `VerifyAccountPasswordAsync()` — same SCrypt used for app passwords |
| `InviteStore` | `src/pds_projects/AccountManager/Db/InviteStore.cs` | Fully functional: `EnsureInviteIsAvailableAsync()`, `RecordInviteUseAsync()` |
| `EmailTokenStore` | `src/pds_projects/AccountManager/Db/EmailTokenStore.cs` | Fully functional with 15-min expiry. Purposes: `confirm_email`, `update_email`, `reset_password`, `delete_account`, `plc_operation` |
| `PlcClient` | `src/projects/DidLib/PlcClient.cs` | `SendOperationAsync()`, `SendTombstoneAsync()` |
| `HandleManager` | `src/pds_projects/` | Handle validation + DNS checks |
| `AppViewProxyController.CreateServiceJwt()` | `src/atompds/Controllers/Xrpc/AppViewProxyController.cs` | Private method — generates `at+jwt` signed with repo signing key. **Needs extraction** into a shared service. |
| `IMailer` / `StubMailer` | `src/pds_projects/Mailer/` | Interface ready for `SmtpMailer` replacement |
| `AuthVerifier` | `src/atompds/Middleware/AuthVerifier.cs` | Validates HS256 bearer JWTs. **Explicitly rejects DPoP** — this is the main P0 auth gap. |
| Auth attributes | `src/atompds/Middleware/` | `[AccessStandard]`, `[AccessFull]`, `[AccessPrivileged]`, `[Refresh]` — used as endpoint metadata, not `[Authorize]` |
| Auth scope enums | `AuthVerifier` | Already defines `AppPass` and `AppPassPrivileged` but they are **never minted** |

### Controller Pattern

All XRPC controllers follow this convention:

```csharp
[ApiController]
[Route("xrpc")]
namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

public class FooController : ControllerBase
{
    // Constructor injection of services

    [HttpPost("com.atproto.server.foo")]
    [AccessStandard] // or [AccessFull], [AccessPrivileged], [Refresh], or no attribute for public endpoints
    public async Task<IActionResult> FooAsync([FromBody] FooInput input) { ... }
}
```

- Namespace follows folder structure under `Controllers/Xrpc/Com/Atproto/{Category}/`
- Route is always `[Route("xrpc")]` on the class, NSID on the method
- DI is constructor injection; services are registered in `src/atompds/Config/ServerConfig.cs:220-286`

---

## Tier 1 — P0: Client Interop (3–4 weeks)

**Goal:** Match the minimum compatibility surface expected by current ATProto clients and services.

### 1A. `getSession` — Status: ☐ Not started

| | |
|---|---|
| **Effort** | S (<1 day) |
| **Priority rationale** | Every client calls this on startup for session restore |
| **Dependencies** | None |

**Implementation:**

1. Create `src/atompds/Controllers/Xrpc/Com/Atproto/Server/GetSessionController.cs`
2. `[HttpGet("com.atproto.server.getSession")]` with `[AccessStandard]`
3. Extract DID from `HttpContext.GetAuthOutput()`, look up account via `AccountRepository`
4. Return `{ did, handle, email, active, status }` (match canonical schema)
5. Add test

**Reference:** Canonical returns `did`, `handle`, `email`, `emailConfirmed`, `active`, `status`.

---

### 1B. `getLatestCommit` — Status: ☐ Not started

| | |
|---|---|
| **Effort** | S (<1 day) |
| **Priority rationale** | Trivial sync parity, repo primitives already exist |
| **Dependencies** | None |

**Implementation:**

1. Create `src/atompds/Controllers/Xrpc/Com/Atproto/Sync/GetLatestCommitController.cs`
2. `[HttpGet("com.atproto.sync.getLatestCommit")]` — public endpoint (no auth attribute)
3. Accept `did` query param, resolve repo head CID + rev from `RepoStore`
4. Return `{ cid, rev }`
5. Add test

---

### 1C. App-Password Suite — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (2–3 days) |
| **Priority rationale** | Clients need app-password login. DB model + refresh-token plumbing already exist; only controllers + login wiring are missing |
| **Dependencies** | None |

**Implementation steps:**

1. **`createAppPassword`**
   - Create `src/atompds/Controllers/Xrpc/Com/Atproto/Server/CreateAppPasswordController.cs`
   - `[AccessPrivileged]` — only main sessions, not app-password sessions
   - Input: `{ name, privileged? }`
   - Generate random 16-char password, SCrypt hash it, store via `AccountStore` into `app_password` table
   - Return `{ name, password }` (plaintext only on creation)
   - Reuse `PasswordStore` SCrypt parameters

2. **`listAppPasswords`**
   - Create `ListAppPasswordsController.cs`
   - `[AccessStandard]`
   - Query `AppPasswords` DbSet for the authenticated DID
   - Return `{{ name, createdAt, privileged }}` — **never** return the hash

3. **`revokeAppPassword`**
   - Create `RevokeAppPasswordController.cs`
   - `[AccessPrivileged]`
   - Input: `{ name }`
   - Delete row from `AppPasswords` where `{did, name}`
   - Call `Auth.RevokeAppPasswordRefreshTokenAsync(did, name)` to invalidate associated refresh tokens
   - Return 200

4. **Wire app-password login into `createSession`**
   - In `AccountRepository.LoginAsync()` (~line 189), after account-password fails:
     - Look up `AppPasswords` for the account DID
     - For each, verify SCrypt against the submitted password
     - On match: mint access token with scope `com.atproto.appPass` (or `appPassPrivileged` if `Privileged=true`)
   - In `AccountRepository.CreateSessionAsync()` (~line 111): check if login was via app password, set scope accordingly

**Existing code to build on:**
- `AppPassword` model: `src/pds_projects/AccountManager/Db/Models.cs:116-125`
- `RefreshToken.AppPasswordName` field
- `Auth.RevokeAppPasswordRefreshTokenAsync()` already exists
- Auth scope enum already has `AppPass` and `AppPassPrivileged`
- `PasswordStore` SCrypt implementation

---

### 1D. Identity Endpoints — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (3–4 days) |
| **Priority rationale** | Required for handle changes and PLC self-management |
| **Dependencies** | None (for most); `requestPlcOperationSignature` uses `EmailTokenStore` + stub mailer which is fine |

**Implementation:**

All go under `src/atompds/Controllers/Xrpc/Com/Atproto/Identity/`.

1. **`updateHandle`** — `[AccessPrivileged]`
   - Input: `{ handle }`
   - Validate handle via `HandleManager`
   - Update PLC directory via `PlcClient`
   - Update local account store
   - Update DNS if needed
   - Return 200

2. **`getRecommendedDidCredentials`** — `[AccessPrivileged]`
   - Return rotation key, signing key, and handle for the authenticated DID
   - Mostly a config + key lookup

3. **`signPlcOperation`** — `[AccessPrivileged]`
   - Input: PLC operation payload
   - Sign with server rotation key using `DidLib` signing primitives
   - Return signed operation

4. **`submitPlcOperation`** — `[AccessPrivileged]`
   - Input: signed PLC operation
   - Forward to `PlcClient.SendOperationAsync()`
   - Return 200

5. **`requestPlcOperationSignature`** — `[AccessPrivileged]`
   - Create email token via `EmailTokenStore` with purpose `plc_operation`
   - Send email via `IMailer` (stub is acceptable for now)
   - Return 200

**Existing code:** `HandleManager`, `PlcClient`, `DidLib` signing, `EmailTokenStore`.

---

### 1E. `getServiceAuth` + Extract `ServiceJwtBuilder` — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (1–2 days) |
| **Priority rationale** | Needed for inter-service auth; the extracted builder is reused by pipethrough (Tier 2) |
| **Dependencies** | None |

**Implementation:**

1. **Extract `ServiceJwtBuilder`** from `AppViewProxyController.CreateServiceJwt()`
   - Move to a shared service in `src/pds_projects/` (e.g., `ServiceJwtBuilder.cs`)
   - Register as scoped in `ServerConfig.RegisterServices()`
   - Update `AppViewProxyController` to use the extracted service

2. **`getServiceAuth`** controller
   - `[AccessStandard]`
   - Input: `{ aud, lxm }` (audience service DID, lexicon method)
   - Generate signed service JWT using `ServiceJwtBuilder`
   - Return `{ token }`

---

### 1F. `reserveSigningKey` + `checkAccountStatus` — Status: ☐ Not started

| | |
|---|---|
| **Effort** | S (1 day) |
| **Priority rationale** | Needed for migration flows (Tier 3) and account status queries |
| **Dependencies** | None |

**Implementation:**

1. **`reserveSigningKey`** — `[AccessPrivileged]`
   - Generate or reuse a repo signing key for the account
   - Store in actor store
   - Return `{ signingKey }`

2. **`checkAccountStatus`** — `[AccessPrivileged]`
   - Query account state (active, taken down, etc.) and repo status (committed, up to date)
   - Return `{ active, status, repoCommit, repoRev, repoRoot }`

---

### 1G. `createAccount` Parity — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (1–2 days) |
| **Priority rationale** | Blocks account import/migration at Tier 3 |
| **Dependencies** | None |
| **Risk** | Medium — touches the most security-sensitive endpoint |

**Implementation:**

1. Extend `CreateAccountController` input model to accept optional `did` and `plcOp`
2. In `AccountRepository.CreateAccountAsync()`:
   - If `did` is provided: skip handle validation, skip PLC creation, use the provided DID directly
   - If `plcOp` is provided: submit it via `PlcClient.SendOperationAsync()`
   - Otherwise: existing flow (create new DID + PLC operation)
3. Accept optional `verificationCode` / `verificationPhone` fields (validate if anti-abuse is configured; ignore for now if not)
4. Add tests for both flows: new account (existing) and imported account (new)

---

### 1H. `describeServer` Metadata — Status: ☐ Not started

| | |
|---|---|
| **Effort** | S (<1 day) |
| **Priority rationale** | Low effort, improves client UX and signaling |
| **Dependencies** | None |

**Implementation:**

1. In `DescribeServerController`, extend the response to include:
   - `links`: `{ privacyPolicy, termsOfService }` from config
   - `contact`: `{ email }` from config
   - `phoneVerificationRequired`: from config
2. Add fields to `ServerEnvironment.cs` if not present

---

### 1I. OAuth Resource-Server Auth: DPoP — Status: ☐ Not started

| | |
|---|---|
| **Effort** | L (5–7 days) |
| **Priority rationale** | Modern clients use OAuth/DPoP exclusively. Without this, OAuth clients cannot authenticate at all |
| **Dependencies** | None — but this is the single largest P0 item |
| **Risk** | High — new auth stack, needs thorough testing |

**Implementation:**

1. **Extend `AuthVerifier`** to accept DPoP-bound access tokens:
   - When `Authorization: DPoP <token>` is received, validate the access token normally
   - Additionally validate the `DPoP` proof header (JWT with `typ: dpop+jwt`)
   - Bind: access token's `cnf.jkt` must match the DPoP proof's JWK thumbprint
   - Manage DPoP nonces (generate, track, validate replay)

2. **OAuth issuer/audience verification:**
   - Fetch auth-server metadata from `.well-known/oauth-authorization-server`
   - Validate token `iss` matches configured auth server
   - Validate token `aud` matches this PDS's DID

3. **Permission mapping:**
   - Map OAuth scopes to existing `[AccessStandard]`/`[AccessFull]`/`[AccessPrivileged]` attributes
   - Ensure the middleware flow continues to work for legacy bearer tokens (backward compatible)

4. **Remove the explicit DPoP rejection** in `AuthVerifier` (the `"DPOP tokens are not currently supported"` throw)

**Reference files:**
- `src/atompds/Middleware/AuthVerifier.cs` — current JWT validation
- `src/atompds/Middleware/AuthMiddleware.cs` — middleware pipeline
- `src/atompds/Controllers/WellKnownController.cs` — `.well-known` endpoints

---

## Tier 2 — P1: Real-World Deployment (6–8 weeks)

**Goal:** Make the PDS usable for real users and modern clients.

### 2A. `SmtpMailer` + Email Flows — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (3–4 days) |
| **Dependencies** | `IMailer` interface, `EmailTokenStore` (both exist) |

**Implementation:**

1. **`SmtpMailer`** — implement `IMailer` using MailKit
   - Add MailKit NuGet dependency
   - Support TLS/STARTTLS
   - Text + HTML email templates
   - Config: SMTP host, port, user, pass, from address — add to `ServerEnvironment.cs`
   - Register in `ServerConfig` based on config toggle (fallback to `StubMailer` if not configured)

2. **Email-dependent endpoints** (all use `EmailTokenStore`):

   | Endpoint | Auth | Purpose | EmailTokenStore purpose |
   |----------|------|---------|------------------------|
   | `confirmEmail` | `[AccessStandard]` | Confirm email with token from email | `confirm_email` |
   | `requestEmailConfirmation` | `[AccessStandard]` | Send confirmation email | `confirm_email` |
   | `requestEmailUpdate` | `[AccessPrivileged]` | Send update token to new address | `update_email` |
   | `updateEmail` | `[AccessPrivileged]` | Update email after token verification | `update_email` |
   | `requestPasswordReset` | Public | Send password reset email | `reset_password` |
   | `resetPassword` | Public | Reset password with valid token | `reset_password` |

3. **Invite code endpoints:**

   | Endpoint | Auth | Notes |
   |----------|------|-------|
   | `createInviteCode` | `[AccessPrivileged]` | `InviteStore` already functional |
   | `createInviteCodes` | `[AccessPrivileged]` | Batch version |
   | `getAccountInviteCodes` | `[AccessStandard]` | List codes for authed account |

---

### 2B. Rate Limiting — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (3–4 days) |
| **Dependencies** | None |

**Implementation:**

1. Add ASP.NET Core `System.Threading.RateLimiting` middleware in `Program.cs`
2. Per-IP global rate limit (sliding window)
3. Stricter per-method limits for: login, email confirmation, password reset, account management
4. Per-user repo write limits for: createRecord, putRecord, deleteRecord, applyWrites
5. Return `Retry-After` header + 429 status
6. Config: `PDS_RATE_LIMITS_ENABLED`, thresholds per category — add to `ServerEnvironment.cs`
7. Add `RateLimitMiddleware` or use built-in `AddRateLimiter()`

---

### 2C. Generic Service-Aware Pipethrough — Status: ☐ Not started

| | |
|---|---|
| **Effort** | L (5–7 days) |
| **Dependencies** | 1E (`ServiceJwtBuilder` extraction) |
| **Risk** | Medium-High — behavioral parity with canonical pipethrough needs careful testing |

**Implementation:**

Replace the fixed 33-method whitelist in `AppViewProxyController` with:

1. **Catchall handler** — any `app.bsky.*` / `chat.bsky.*` / `com.atproto.moderation.*` not handled by a local controller gets proxied
2. **`atproto-proxy` header support** — parse `did:web:...#service-id` to resolve target service URL
3. **Service JWT generation** — use extracted `ServiceJwtBuilder`
4. **Protected-method exclusions** — account management methods must not be proxied
5. **Report/mod-service routing** — route moderation to configured report service
6. **Broader chat coverage** — add missing `chat.bsky.*` handlers

**Reference:** Current proxy is in `src/atompds/Controllers/Xrpc/AppViewProxyController.cs`.

---

### 2D. Signup Anti-Abuse — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (2–3 days) |
| **Dependencies** | 1H (`describeServer` metadata), 2A (email verification) |

**Implementation:**

1. hCaptcha verification option for account creation
2. Phone verification signaling in `describeServer`
3. Policy/contact metadata population
4. Config: hCaptcha secret, phone verification toggle

---

### 2E. OAuth Provider / Entryway Integration — Status: ☐ Not started

| | |
|---|---|
| **Effort** | XL (2+ weeks) |
| **Dependencies** | 1I (resource-server side) |
| **Risk** | High — largest single work item |

**Implementation (see GAP.md section 2.3 for details):**

1. Authorization server metadata (replace `.well-known/oauth-authorization-server` stub)
2. Authorization endpoint (consent + auth-code issuance)
3. Token endpoint (code exchange + refresh)
4. PKCE support (S256 challenge/verification)
5. DPoP provider-side flow
6. OAuth session store (grants, refresh chains)
7. Trusted clients / entryway configuration

---

### 2F. Read-After-Write Consistency — Status: ☐ Not started

| | |
|---|---|
| **Effort** | L (5–7 days) |
| **Dependencies** | 2C (pipethrough must be in place to patch responses) |
| **Risk** | Medium — correctness around ordering and TTL |

**Implementation:**

1. Write snapshot cache — cache recent writes per DID with TTL
2. Proxy response patching — merge local writes into proxied App View responses
3. Catch-up invalidation — expire patches once upstream has indexed

---

## Tier 3 — P2: Operations & Admin (4–6 weeks)

**Goal:** Make the PDS operable and support migration/reporting workflows.

### 3A. Admin Endpoints — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (3–4 days) |
| **Dependencies** | None (admin auth via HTTP Basic already works) |

All under `Controllers/Xrpc/Com/Atproto/Admin/`. All require admin auth (HTTP Basic).

| Endpoint | Effort | Notes |
|----------|--------|-------|
| `getAccountInfo` | S | Query by DID/handle |
| `getAccountInfos` | S | Batch version |
| `deleteAccount` | M | Admin-forced deletion |
| `updateAccountHandle` | M | Admin handle change + PLC update |
| `updateAccountEmail` | S | Direct email update |
| `updateAccountPassword` | S | Direct password reset |
| `getSubjectStatus` | S | Moderation/takedown status |
| `updateSubjectStatus` | M | Apply/remove takedown |
| `enableAccountInvites` | S | Re-enable invite creation |
| `disableAccountInvites` | S | Disable invite creation |
| `disableInviteCodes` | S | Disable specific codes |
| `getInviteCodes` | S | List with filtering |
| `sendEmail` | S | Send via `IMailer` |

---

### 3B. `pdsadmin` Alignment — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (2 days) |
| **Dependencies** | 3A (admin endpoints must exist first) |

- Ensure `src/pdsadmin/Program.cs` calls match actual server endpoints
- Add capability checks / fail-fast when endpoints are missing

---

### 3C. Moderation: `createReport` + Report Service — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (3–4 days) |
| **Dependencies** | 2C (pipethrough / service auth) |

- `com.atproto.moderation.createReport` — forward to configured report service with service auth
- Config: `PDS_REPORT_SERVICE_URL` / DID — add to `ServerEnvironment.cs`
- Error propagation from upstream report service

---

### 3D. Background Job Queue — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (2–3 days) |
| **Dependencies** | 2A (mailer for async email) |

- `Channel<T>`-based background worker abstraction
- Move email sending off the request path
- Move relay/crawler notifications off the request path
- Register as hosted service in `Program.cs`

---

### 3E. Account Migration / `importRepo` — Status: ☐ Not started

| | |
|---|---|
| **Effort** | XL (2+ weeks) |
| **Dependencies** | 1G (`createAccount` import support) |
| **Risk** | High — most complex new feature |

- `com.atproto.repo.importRepo` — import full repo from CAR file
- Imported-account creation (accept `did`/`plcOp`)
- Migration validation (DID/handle alignment)
- Account portability flows (source deactivation + destination activation)

---

### 3F. `listMissingBlobs` — Status: ☐ Not started

| | |
|---|---|
| **Effort** | M (1–2 days) |
| **Dependencies** | None |

- Scan records for blob references, check blob store, return missing list

---

## Tier 4 — P3: Polish (2–3 weeks)

**Goal:** Close remaining low-priority parity gaps. All independent, can be done in any order.

| Item | Effort | Notes |
|------|--------|-------|
| Redis scratch integration | M | Rate-limit counters, cache. Config: `PDS_REDIS_*` |
| `app.bsky.notification.registerPush` | M | Push notification handler |
| `com.atproto.temp.checkSignupQueue` | S | Signup queue status |
| `GET /tls-check` | S | Caddy on-demand TLS handle verification |
| `GET /` landing page | S | Basic service info |
| `GET /robots.txt` | S | Canonical crawler behavior |

---

## Validation Checklist

After completing any item, run:

```bash
dotnet build atompds.slnx
dotnet test atompds.slnx
```

For focused testing:

```bash
dotnet test test/ActorStore.Tests/ActorStore.Tests.csproj
dotnet test test/Common.Tests/Common.Tests.csproj
```

For dependency hygiene:

```bash
dotnet list atompds.slnx package --vulnerable --include-transitive
```

---

## Key Reminders

- **Behavioral parity, not architectural parity.** The canonical PDS is TypeScript/Express/Kysely. This is C#/.NET 10/ASP.NET Core/EF Core. Match the behavior, not the structure.
- **Follow existing patterns.** Constructor injection, `[Route("xrpc")]` + NSID method routes, auth attributes, namespace-to-folder mapping.
- **Don't introduce new libraries** without checking the codebase first. MailKit for SMTP is a new addition (Tier 2A); most other work uses existing dependencies.
- **`**/atompds/appsettings.*.json` is gitignored** — never commit local config with secrets.
- **The `pdsadmin` CLI** currently calls endpoints that don't exist on this server. That mismatch is tracked as item 3B.
