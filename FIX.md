# FIX.md -- Prioritized Remediation Guide

> **Status:** Build passes (0 errors, 27 warnings). All 328 tests pass. No vulnerable packages.
> This file is ordered by priority. Each item includes exact file locations, what to change, and verification steps.

---

## CRITICAL (fix immediately)

### C1. Hardcoded Admin Password

**File:** `src/atompds/Config/ServerConfig.cs:315`
**File:** `src/atompds/Config/ServerEnvironment.cs` (add property)

The admin password is the literal string `"secret"`. Any client sending `Authorization: Basic admin:secret` has full admin access.

**Fix:**
1. Add `public string? PDS_ADMIN_PASSWORD { get; set; }` to `ServerEnvironment`
2. In `ServerConfig`, read `env.PDS_ADMIN_PASSWORD` and throw on startup if not set in production
3. Pass the configured value to `AuthVerifierConfig` instead of `"secret"`
4. Fallback to `"secret"` only when `PDS_DEV_MODE=true`

**Verify:** Start app without `PDS_ADMIN_PASSWORD` set, confirm admin endpoints reject `Basic admin:secret`.

---

### C2. StubMailer Logs Security Tokens in Plaintext

**File:** `src/pds_projects/Mailer/StubMailer.cs:21,27,33,39,45`

When SMTP is not configured (the default), all password reset, account deletion, and email confirmation tokens are logged at `Information` level with their full values.

**Fix:** Replace `{token}` in all five `LogInformation` calls with a redacted placeholder:
```csharp
_logger.LogInformation("[STUB] Sending account delete email to {to} with token [REDACTED]", to);
```
Or use `logger.LogInformation("... with token {token}", "***")` to keep the structured logging parameter.

**Verify:** Grep `StubMailer.cs` for `{token}` — should be zero matches.

---

### C3. CORS Middleware Ordered Incorrectly

**File:** `src/atompds/Program.cs:67-81`

`UseCors()` is registered after routing, auth middleware, and endpoint mapping. Browser preflight `OPTIONS` requests hit auth middleware (401) before CORS headers are added.

**Fix:** Move `app.UseCors(...)` to before `app.UseRouting()`:
```csharp
app.UseCors(cors => cors.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseRouting();
if (environment.PDS_RATE_LIMITS_ENABLED)
{
    app.UseRateLimiter();
}
app.MapEndpoints(...);
app.UseExceptionHandler("/error");
app.UseAuthMiddleware();
app.UseNotFoundMiddleware();
app.UseWebSockets();
```

**Verify:** Build and run existing tests: `dotnet test --solution atompds.slnx`

---

### C4. Sequencer/Outbox Race Condition

**File:** `src/pds_projects/Sequencer/Outbox.cs:133-149`

`_caughtUp` is a plain `bool` read/written from multiple threads without synchronization. The code comments acknowledge the race. This can cause firehose event reordering.

**Fix:**
1. Mark `_caughtUp` as `volatile bool`
2. Or better: use `lock` or `ReaderWriterLockSlim` around the read/write of `_caughtUp` and the subsequent buffer writes, ensuring the cutover check and buffer drain are atomic
3. The `CutoverBuffer.Clear()` at line 95 is also not atomic with concurrent enqueues — wrap in a lock or use `Drain()` pattern instead

**Verify:** Build. Consider adding a concurrency test that exercises backfill + live events simultaneously.

---

### C5. No CI/CD Pipeline

**File:** `.github/workflows/` (does not exist)

**Fix:** Create `.github/workflows/build.yml`:
```yaml
name: Build & Test
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build --solution atompds.slnx
      - run: dotnet test --solution atompds.slnx
```

Also add `.github/dependabot.yml` for NuGet dependency scanning.

**Verify:** Push and confirm the workflow runs green.

---

## HIGH (fix soon)

### H1. Rate Limiting Disabled by Default

**File:** `src/atompds/Config/ServerEnvironment.cs:118`

`PDS_RATE_LIMITS_ENABLED` defaults to `false`. Combined with C1 (hardcoded admin password), there is no brute-force protection.

**Fix:** Change default to `true` or at minimum document the risk in `appsettings.Development.json.example`. If changing to `true`, verify the rate limit values in `src/atompds/Middleware/RateLimitMiddleware.cs` are sensible for production.

**Verify:** `dotnet build --solution atompds.slnx`

---

### H2. SSRF Protection Not Enforced

**File:** `src/pds_projects/Config/ProxyConfig.cs:5` — flag exists but is never read
**File:** `src/atompds/Endpoints/Xrpc/AppViewProxyEndpoints.cs:193-194` — makes HTTP requests to DID-resolved URLs

**Fix:**
1. In `AppViewProxyEndpoints.cs`, before making the proxied request, check `ProxyConfig.DisableSsrfProtection`
2. If not disabled, validate the resolved URL against private/internal IP ranges (loopback, link-local, 10.x, 172.16-31.x, 192.168.x, 169.254.x, etc.)
3. Use `System.Net.IPAddress` to check the resolved addresses, or use a library

**Verify:** Build and run tests.

---

### H3. OAuth redirect_uri Not Validated

**File:** `src/atompds/Endpoints/OAuth/OAuthAuthorizeEndpoints.cs:48-49,98`

The `redirect_uri` query parameter is used directly in `Results.Redirect()` without any validation against registered client redirect URIs.

**Fix:**
1. Define an allowlist of valid redirect URIs (configured via `ServerEnvironment`)
2. Before redirecting, validate `redirect_uri` against the allowlist
3. Return 400 if the redirect_uri is not registered

**Verify:** Build. Add a test that sends an invalid `redirect_uri` and asserts 400.

---

### H4. Synchronous Blocking in Endpoint Handler

**File:** `src/atompds/Endpoints/Xrpc/Com/Atproto/Server/RefreshSessionEndpoints.cs:46-47`

```csharp
var didDoc = didDocTask.Result;
var rotated = rotateTask.Result;
```

**Fix:** Replace with `await`:
```csharp
var didDoc = await didDocTask;
var rotated = await rotateTask;
```

**Verify:** Build. VSTHRD103 warning at lines 46-47 should disappear.

---

### H5. SequencerRepository Scoped but Spawns Background Polling

**File:** `src/pds_projects/Sequencer/SequencerRepository.cs:35` — `Task.Run(PollTaskAsync)` in constructor
**File:** `src/atompds/Config/ServerConfig.cs:324` — registered as `AddScoped`

Every HTTP request that injects `SequencerRepository` creates a new polling task.

**Fix:**
1. Extract the polling logic into a separate `SequencerPollingService : BackgroundService` registered as a hosted service
2. Register `SequencerRepository` as singleton (its dependency `SequencerDb` is via `IDbContextFactory`, which is safe for singleton use)
3. Or: keep scoped but inject a shared `Channel<ISeqEvt>` from a singleton, so only one poller exists

**Verify:** Build and run `atompds.Tests`.

---

### H6. Missing Audit Logging for Destructive Operations

**Files:**
- `src/atompds/Endpoints/Xrpc/Com/Atproto/Server/DeleteAccountEndpoints.cs:58`
- `src/atompds/Endpoints/Xrpc/Com/Atproto/Admin/AdminDeleteAccountEndpoints.cs:20`
- `src/pds_projects/AccountManager/Db/AccountStore.cs:178-184, 205-225, 247-267`
- `src/pds_projects/AccountManager/AccountRepository.cs:214-268` (login failures)

**Fix:** Add `ILogger` injection and structured logging for:
- Account deletion (log DID)
- Account deactivation/activation (log DID)
- Password changes (log DID)
- Email changes (log DID, old/new email)
- Failed login attempts (log DID or email, IP if available)
- Failed JWT validation (in `AuthVerifier.cs:637`)

**Verify:** Build. Grep for `LogInformation` or `LogWarning` at the modified locations.

---

### H7. Proxy Logs Full Response Bodies

**File:** `src/atompds/Endpoints/Xrpc/AppViewProxyEndpoints.cs:224,256,301`

Full API response content logged at `Information` level, potentially exposing PII.

**Fix:**
1. Change log level from `LogInformation` to `LogDebug`
2. Replace `{content}` with response length or omit entirely:
```csharp
logger.LogDebug("[PROXY][{status}] {path} via {serviceDid}", status, path, serviceDid);
```

**Verify:** Build. Grep for `[PROXY]` logs to confirm no full response body logging at Information level.

---

### H8. BuildServiceProvider() Anti-Pattern

**File:** `src/atompds/Config/ServerConfig.cs:343`

```csharp
services.AddSingleton<IMailer>(new SmtpMailer(smtpConfig,
    services.BuildServiceProvider().GetRequiredService<ILogger<SmtpMailer>>()));
```

**Fix:** Use factory pattern:
```csharp
services.AddSingleton<IMailer>(sp => new SmtpMailer(smtpConfig,
    sp.GetRequiredService<ILogger<SmtpMailer>>()));
```

**Verify:** Build. Check that SmtpMailer resolves correctly.

---

### H9. Massive Test Coverage Gaps

**Zero tests exist for these critical areas:**

| Area | Priority | Suggested test file |
|------|----------|-------------------|
| `src/projects/Crypto/` (key generation, signing, verification) | Critical | `test/Crypto.Tests/` |
| `src/projects/Repo/` (MST insert/delete/walk, CAR encoding, commits) | Critical | `test/Repo.Tests/` |
| `src/pds_projects/Sequencer/` (event ordering, Outbox cutover, backfill) | Critical | `test/Sequencer.Tests/` |
| `src/pds_projects/AccountManager/` (Auth, password hashing, token rotation) | Critical | `test/AccountManager.Tests/` |
| `src/projects/Identity/` (DID resolution, caching, handle resolution) | High | `test/Identity.Tests/` |
| `src/projects/Handle/` (validation, normalization) | High | `test/Handle.Tests/` |
| `src/projects/DidLib/` (PLC operations) | High | `test/DidLib.Tests/` |
| `src/atompds/Services/` (BackgroundJobQueue, OAuthSessionStore, WriteSnapshotCache) | High | `test/atompds.Tests/Services/` |
| `src/pds_projects/BlobStore/` (disk + S3 paths, temp-to-permanent lifecycle) | Medium | `test/BlobStore.Tests/` |
| `src/atompds/Middleware/` (AuthVerifier, RateLimitMiddleware unit tests) | Medium | `test/atompds.Tests/Middleware/` |

**Existing integration tests are shallow:** ~85% of `test/atompds.Tests/` tests only verify route existence and auth gating. No tests exercise full business logic flows (create account, create session, write record, read record).

**Fix approach:**
1. Start with `Crypto.Tests` and `Repo.Tests` — these are foundational libraries with no external dependencies
2. Add `Sequencer.Tests` — test Outbox cutover, event ordering, concurrent consumers
3. Add `AccountManager.Tests` — test password hashing, JWT creation/validation, refresh token rotation (use in-memory SQLite)
4. For integration tests, add test infrastructure: database seeding, account factory helpers, authenticated client helpers
5. Use TUnit (`[Test]`, `Assert.That(...).IsEqualTo(...)`) — NOT xUnit

**Verify:** `dotnet test --solution atompds.slnx`

---

## MEDIUM (plan to address)

### M1. Config Records Missing `required` Keyword (9 CS8618 warnings)

**Files:** `src/pds_projects/Config/DatabaseConfig.cs`, `ActorStoreConfig.cs`, `IdentityConfig.cs`, `BskyAppViewConfig.cs`, `BlobstoreConfig.cs`

**Fix:** Add `required` to all non-nullable `init`-only `string` and `List<string>` properties. Follow the pattern already used by `ServiceConfig.cs`, `SecretsConfig.cs`, and `SubscriptionConfig.cs`.

**Verify:** `dotnet build --solution atompds.slnx` — CS8618 warnings for these files should disappear.

---

### M2. No JWT Expiration Validation in Bearer Token Path

**File:** `src/atompds/Middleware/AuthVerifier.cs:612-641`

`jose-jwt`'s `JWT.Verify()` only verifies the HMAC signature. The `exp` claim is not checked for the non-OAuth bearer path (the OAuth path does check `exp`).

**Fix:** After `JWT.Verify()`, decode the payload and check `exp > now`. Or migrate from `jose-jwt` to `Microsoft.IdentityModel.JsonWebTokens` (already in the project) for the bearer path, which validates lifetime by default.

**Verify:** Create a token, wait for expiration, confirm it is rejected.

---

### M3. No SQLite WAL Mode or busy_timeout on Global DBs

**Files:** `src/atompds/Config/ServerConfig.cs:262-263,323`

AccountManagerDb and SequencerDb connection strings lack `PRAGMA journal_mode=WAL` and `PRAGMA busy_timeout`.

**Fix:** Append to the SQLite connection strings:
```
Data Source={path};Mode=ReadWriteCreate
```
And execute after connection:
```csharp
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;
PRAGMA synchronous=NORMAL;
```
Use `DbContextOptionsBuilder` with `ExecuteSqlRaw` in a database seed or via `SqliteConnection` initialization.

**Verify:** Build and run tests.

---

### M4. No Password Strength Enforcement

**Files:** `src/atompds/Endpoints/Xrpc/Com/Atproto/Server/CreateAccountEndpoints.cs:219`, `ResetPasswordEndpoints.cs:19-26`

**Fix:** Add validation (minimum 8 characters, not empty) before hashing. Throw `XRPCError(new InvalidRequestErrorDetail(...))`.

**Verify:** Add tests for short/empty passwords.

---

### M5. BlobStore Path Uses Raw DID Without Traversal Checks

**File:** `src/pds_projects/BlobStore/DiskBlobStore.cs:36,46-47`

`Did` is used directly in `Path.Join` without sanitization. A malicious DID containing `../` could cause path traversal.

**Fix:** Add a validation method that rejects DIDs containing path separators or `..`:
```csharp
if (Did.Contains("..") || Did.Contains('/') || Did.Contains('\\'))
    throw new ArgumentException("Invalid DID");
```

**Verify:** Build.

---

### M6. Exception Swallowing (15+ locations)

Bare `catch` blocks with no logging across:
- `OAuthAuthorizeEndpoints.cs:74` — OAuth auth failure silently ignored
- `OAuthTokenEndpoints.cs:198` — JWK thumbprint extraction returns null silently
- `AuthVerifier.cs:555` — JWT section decode returns null silently
- `AccountManager/Auth.cs:191` — refresh token rotation failure retried silently
- `HandleResolver.cs:57,83` — DNS/HTTP resolution returns null silently
- `SequencerRepository.cs:84` — DB error triggers backoff with no logging
- `SqlRepoTransactor.cs:185` — DB read failure returns null silently

**Fix:** Add `logger.LogDebug(ex, "...")` or `logger.LogWarning(ex, "...")` in each catch block before returning null or continuing.

**Verify:** Build.

---

### M7. Raw `Exception` Throws Bypass XRPC Error Formatting

**Files:**
- `ApplyWritesEndpoints.cs:291` — `throw new Exception("Invalid write type.")`
- `AuthVerifier.cs:117` — `throw new Exception("Response has already started")`
- `AuthVerifier.cs:255-256` — `throw new Exception()` (no message)
- `AuthVerifier.cs:565` — `throw new Exception("Invalid auth type")`
- `ServiceJwtBuilder.cs:24` — `throw new Exception("Signing key is not exportable")`

**Fix:** Replace with appropriate `XRPCError` throws:
```csharp
throw new XRPCError(new InvalidRequestErrorDetail("Invalid write type."));
```
For internal errors, use `InternalServerErrorDetail` or a new error detail type.

**Verify:** Build.

---

### M8. Health Endpoint is a No-Op

**File:** `src/atompds/Endpoints/Xrpc/HealthEndpoints.cs:14-17`

Returns static version string. Does not check database connectivity, blob store, or downstream services.

**Fix:** Inject `AccountManagerDb` and attempt a simple query (e.g., `context.Database.CanConnectAsync()`). Optionally check blob store path accessibility. Return 503 if any check fails.

**Verify:** Build. Test that `_health` returns 200 when DB is available.

---

### M9. No Observability Infrastructure

No OpenTelemetry, distributed tracing, metrics, or correlation ID propagation anywhere in the codebase.

**Fix:** (Phased approach)
1. Add `OpenTelemetry` tracing with ASP.NET Core instrumentation
2. Add `OpenTelemetry` metrics with HTTP request duration counters
3. Add Prometheus exporter endpoint
4. Propagate trace context in `AppViewProxyEndpoints.cs` proxy calls
5. Add `X-Request-Id` / `X-Correlation-Id` middleware

**Verify:** Build. Optionally verify traces appear in collector.

---

### M10. Unused NuGet Packages

**Files:** `Directory.Packages.props`, `src/atompds/atompds.csproj`

These packages are listed but never used in source code:
- `Newtonsoft.Json` — not imported in any `.csproj`, zero `using Newtonsoft` in codebase
- `System.Drawing.Common` — not imported in any `.csproj`
- `Scalar.AspNetCore` — referenced in `atompds.csproj:23` but never invoked
- `Microsoft.AspNetCore.OpenApi` — referenced in `atompds.csproj:16` but never invoked

**Fix:**
1. Remove `Newtonsoft.Json` and `System.Drawing.Common` from `Directory.Packages.props`
2. Remove `Scalar.AspNetCore` and `Microsoft.AspNetCore.OpenApi` from both `atompds.csproj` and `Directory.Packages.props` (or configure them if OpenAPI docs are desired)

**Verify:** `dotnet build --solution atompds.slnx`

---

### M11. AGENTS.md Incorrectly References xUnit

**File:** `AGENTS.md:16,88`

States "xUnit test projects" and "Add or update xUnit tests". The project uses TUnit.

**Fix:**
- Line 16: Change `xUnit test projects plus` to `TUnit test projects plus`
- Line 88: Change `Add or update xUnit tests` to `Add or update TUnit tests`
- Add note: "Use `[Test]` attribute (not `[Fact]`). Use `Assert.That(...).IsEqualTo(...)` (not `Assert.Equal`)."

**Verify:** Read the file.

---

### M12. Background Job DI Split from RegisterServices()

**File:** `src/atompds/Program.cs:49-53`

Background job registrations are in `Program.cs` while all other DI wiring is in `ServerConfig.RegisterServices()`.

**Fix:** Move lines 49-53 from `Program.cs` into `ServerConfig.RegisterServices()`. Pass the `ChannelWriter` dependency to `SequencerRepository` registration within the same method.

**Verify:** Build and run tests.

---

### M13. HTTP Logging Configured but Middleware Commented Out

**File:** `src/atompds/Program.cs:37-42,85`

`AddHttpLogging()` service is registered but `app.UseHttpLogging()` is commented out.

**Fix:** Either remove the `AddHttpLogging()` configuration block (lines 37-42) or uncomment line 85 to enable it.

**Verify:** Build.

---

## Recommended Execution Order

1. C1 (admin password) + C2 (token logging) + C3 (CORS) — quick fixes, immediate security impact
2. C5 (CI/CD) — enables automated validation for all subsequent changes
3. C4 (Outbox race) + H5 (SequencerRepository lifetime) — concurrency fixes
4. H4 (sync blocking) + H8 (BuildServiceProvider) + M1 (required keyword) + M10 (unused packages) — clean code warnings
5. H6 (audit logging) + H7 (proxy logging) + M6 (exception swallowing) — operational safety
6. H1 (rate limiting) + H2 (SSRF) + H3 (OAuth redirect) + M2 (JWT exp) + M5 (path traversal) — security hardening
7. H9 (test coverage) — start with Crypto, Repo, Sequencer tests
8. M3 (SQLite WAL) + M4 (password strength) + M8 (health checks) — production readiness
9. M7 (raw exceptions) + M11 (AGENTS.md) + M12 (DI split) + M13 (HTTP logging) — cleanup
10. M9 (observability) — larger effort, plan separately
