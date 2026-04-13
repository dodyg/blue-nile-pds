# Refactor Plan: MVC Controllers → ASP.NET Core Minimal API (.NET 10)

## 1. Overview

Refactor all 65 MVC controllers (`ControllerBase` + `[ApiController]` + attribute routing) to ASP.NET Core Minimal API endpoint definitions. The project already has 3 minimal API endpoints in `Program.cs` and targets `net10.0` with `LangVersion=preview`.

**Scope:** 65 controllers, ~110 action methods, ~6,200 lines of controller code.

**Not in scope:** Service layer, DI registration, database code, CarpaNet serialization models.

---

## 2. Why Minimal APIs

| Concern | MVC Controllers | Minimal APIs |
|---|---|---|
| Allocation per request | `ControllerBase` instance | Static/lambda — no allocation |
| Startup overhead | Controller discovery, model binding infrastructure | Direct `MapGet`/`MapPost` calls |
| DI resolution | Constructor injection only | Method parameter injection |
| Auth attributes | Custom `[AdminToken]`, `[AccessStandard]`, etc. as metadata | `.WithMetadata(...)` or `.AddEndpointFilter(...)` |
| Readability | One class per action, 30–780 lines | One static method per action, co-located |
| .NET 10 alignment | Legacy pattern | Idiomatic, recommended approach |

---

## 3. Target Directory Structure

Keep the existing XRPC namespace mapping. Replace per-controller `.cs` files with per-endpoint-group static classes. One file per ATProto method (one class, one `Handle` method).

```
src/atompds/
  Endpoints/
    RootEndpoints.cs                        ← GET /, GET /robots.txt, GET /tls-check (moved from Program.cs)
    ErrorEndpoints.cs                       ← GET/POST /error
    WellKnownEndpoints.cs                   ← GET .well-known/*
    OAuth/
      OAuthTokenEndpoints.cs
      OAuthAuthorizeEndpoints.cs
      OAuthClientMetadataEndpoints.cs
    Xrpc/
      HealthEndpoints.cs
      AppView/
        AppViewProxyEndpoints.cs            ← the large proxy controller, split internally
      Com/
        Atproto/
          Admin/
            AccountInvitesAdminEndpoints.cs
            AdminDeleteAccountEndpoints.cs
            DisableInviteCodesAdminEndpoints.cs
            GetAccountInfoEndpoints.cs
            GetAccountInfosEndpoints.cs
            GetInviteCodesAdminEndpoints.cs
            SendEmailAdminEndpoints.cs
            SubjectStatusEndpoints.cs
            UpdateAccountEmailAdminEndpoints.cs
            UpdateAccountHandleAdminEndpoints.cs
            UpdateAccountPasswordAdminEndpoints.cs
          Identity/
            ResolveHandleEndpoints.cs
            UpdateHandleEndpoints.cs
            GetRecommendedDidCredentialsEndpoints.cs
            RequestPlcOperationSignatureEndpoints.cs
            SignPlcOperationEndpoints.cs
            SubmitPlcOperationEndpoints.cs
          Moderation/
            CreateReportEndpoints.cs
          Repo/
            ApplyWritesEndpoints.cs          ← getRecord, putRecord, deleteRecord, createRecord, applyWrites
            ListRecordsEndpoints.cs
            DescribeRepoEndpoints.cs
            BlobEndpoints.cs                ← uploadBlob (file upload)
            ImportRepoEndpoints.cs          ← importRepo (CAR file upload)
            ListMissingBlobsEndpoints.cs
          Server/
            CreateAccountEndpoints.cs
            CreateSessionEndpoints.cs
            GetSessionEndpoints.cs
            RefreshSessionEndpoints.cs
            DeleteSessionEndpoints.cs
            DeleteAccountEndpoints.cs
            DescribeServerEndpoints.cs
            CreateInviteCodeEndpoints.cs
            CreateInviteCodesEndpoints.cs
            GetAccountInviteCodesEndpoints.cs
            CheckAccountStatusEndpoints.cs
            ActivateAccountEndpoints.cs
            DeactivateAccountEndpointsEndpoints.cs
            CreateAppPasswordEndpoints.cs
            ListAppPasswordsEndpoints.cs
            RevokeAppPasswordEndpoints.cs
            UpdateEmailEndpoints.cs
            ConfirmEmailEndpoints.cs
            RequestEmailConfirmationEndpoints.cs
            RequestEmailUpdateEndpoints.cs
            RequestPasswordResetEndpoints.cs
            ResetPasswordEndpoints.cs
            ReserveSigningKeyEndpoints.cs
            GetServiceAuthEndpoints.cs
          Sync/
            SubscribeReposEndpoints.cs      ← WebSocket streaming
            ListReposEndpoints.cs
            GetRepoEndpoints.cs             ← streaming CAR response
            GetBlocksEndpoints.cs           ← streaming CAR response
            GetBlobEndpoints.cs             ← streaming blob response
            ListBlobsEndpoints.cs
            GetRecordEndpoints.cs           ← streaming CAR response
            GetLatestCommitEndpoints.cs
            GetRepoStatusEndpoints.cs
          Temp/
            CheckSignupQueueEndpoints.cs
```

---

## 4. Endpoint Class Pattern

Each endpoint file follows a consistent pattern: a `static` class with `static async Task<IResult> Handle(...)` methods and a `Map` extension method that registers all routes onto a `RouteGroupBuilder`.

### 4.1 Simple Endpoint (before → after)

**Before (MVC controller):**
```csharp
[ApiController]
[Route("xrpc")]
public class DeleteSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<DeleteSessionController> _logger;

    public DeleteSessionController(AccountRepository accountRepository,
        ILogger<DeleteSessionController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.deleteSession")]
    [Refresh]
    public async Task<IActionResult> DeleteSessionAsync()
    {
        var auth = HttpContext.GetRefreshOutput();
        var tokenId = auth.RefreshCredentials.TokenId;
        await _accountRepository.RevokeRefreshTokenAsync(tokenId);
        return Ok();
    }
}
```

**After (Minimal API):**
```csharp
namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class DeleteSessionEndpoints
{
    public static RouteGroupBuilder MapDeleteSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.deleteSession", Handle)
            .WithMetadata(new RefreshAttribute());

        return group;
    }

    public static async Task<IResult> Handle(
        HttpContext context,
        AccountRepository accountRepository,
        ILogger<DeleteSessionEndpoints> logger)
    {
        var auth = context.GetRefreshOutput();
        var tokenId = auth.RefreshCredentials.TokenId;
        await accountRepository.RevokeRefreshTokenAsync(tokenId);
        return Results.Ok();
    }
}
```

### 4.2 Key Changes Per Endpoint

| MVC Pattern | Minimal API Replacement |
|---|---|
| `ControllerBase` inheritance | Remove; use static class |
| `[ApiController]` | Remove (minimal APIs have automatic validation) |
| `[Route("xrpc")]` on class | `RouteGroupBuilder` from `app.MapGroup("xrpc")` |
| `[HttpGet("...")]` / `[HttpPost("...")]` | `group.MapGet("...", Handle)` / `group.MapPost("...", Handle)` |
| `[AccessStandard]` | `.WithMetadata(new AccessStandardAttribute())` |
| `[AccessStandard(true, true)]` | `.WithMetadata(new AccessStandardAttribute(true, true))` |
| `[AdminToken]` | `.WithMetadata(new AdminTokenAttribute())` |
| `[Refresh]` | `.WithMetadata(new RefreshAttribute())` |
| `[EnableRateLimiting("auth-sensitive")]` | `.RequireRateLimiting("auth-sensitive")` |
| `[DisableRateLimiting]` | `.DisableRateLimiting()` |
| Constructor injection | Method parameter injection |
| `HttpContext` via `ControllerBase.HttpContext` | `HttpContext context` parameter |
| `Ok(obj)` | `Results.Ok(obj)` |
| `Ok()` | `Results.Ok()` |
| `return Ok();` (no body) | `Results.Ok()` |

---

## 5. Auth Attribute Migration Strategy

The existing `AuthMiddleware` reads endpoint metadata (`AdminTokenAttribute`, `AccessStandardAttribute`, etc.) and calls the corresponding `AuthVerifier` method. **This mechanism works unchanged with Minimal APIs** because `.WithMetadata(...)` adds the same attribute objects to endpoint metadata.

No changes needed to:
- `AuthMiddleware.cs` — continues reading `endpoint.Metadata.GetMetadata<T>()`
- Auth attribute classes — continue as plain `Attribute` subclasses
- `AuthMiddlewareExtensions` — `GetAuthOutput()` / `GetRefreshOutput()` continue to work on `HttpContext`

**Migration rule:** Replace `[SomeAuth]` on actions with `.WithMetadata(new SomeAuthAttribute(...))` on the `MapXxx` call.

---

## 6. Special Endpoint Patterns

### 6.1 Streaming Responses (CAR / Blob)

5 controllers write directly to `Response.Body` with custom content types. In Minimal APIs, use `Results.Stream()` or `Results.Bytes()`:

```csharp
// Before: writes to Response.Body directly
Response.ContentType = "application/vnd.ipld.car";
await Response.Body.WriteAsync(carBytes, ct);

// After: return Results.Stream
return Results.Stream(stream, "application/vnd.ipld.car");
```

Affected endpoints: `GetRepo`, `GetBlocks`, `GetBlob`, `GetRecord` (Sync namespace).

### 6.2 File Upload (Request.Body)

2 controllers read from `Request.Body` (`BlobController.UploadBlobAsync`, `ImportRepoController.ImportRepoAsync`). In Minimal APIs, accept `HttpRequest request` and read `request.Body`:

```csharp
public static async Task<IResult> Handle(
    HttpRequest request,
    HttpContext context,
    ServerConfig serverConfig,
    ActorRepositoryProvider actorRepositoryProvider)
{
    var contentLength = request.ContentLength;
    var stream = request.Body;
    // ... same logic
}
```

### 6.3 WebSocket (SubscribeRepos)

`SubscribeReposController` uses `HttpContext.WebSockets.AcceptWebSocketAsync()`. In Minimal APIs, accept `HttpContext` as a parameter and use it the same way. The endpoint **must** be `MapGet` (WebSockets start as HTTP GET upgrades):

```csharp
group.MapGet("com.atproto.sync.subscribeRepos", Handle);
```

The existing `app.UseWebSockets()` middleware is unchanged.

### 6.4 AppView Proxy (Catchall Routes)

`AppViewProxyController` has a catchall route `{nsid}` matching `app.bsky.*`, `chat.bsky.*`, `com.atproto.moderation.*`. In Minimal APIs:

```csharp
group.MapGet("{nsid}", HandleCatchall)
     .WithMetadata(new AccessStandardAttribute());
group.MapPost("{nsid}", HandleCatchall)
     .WithMetadata(new AccessStandardAttribute());
```

The `nsid` parameter is captured as a route parameter: `string nsid`.

---

## 7. Program.cs Changes

### 7.1 Remove Controller Registration

```diff
- builder.Services.AddControllers().AddJsonOptions(options =>
- {
-     options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
- });
```

### 7.2 Add Minimal API JSON Options

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
});
```

### 7.3 Replace MapControllers with Endpoint Registration

```diff
- app.MapControllers();
+ app.MapEndpoints(serverConfig, identityConfig, environment);
```

### 7.4 New Extension Method for Endpoint Registration

Create `src/atompds/Endpoints/EndpointRegistration.cs`:

```csharp
public static class EndpointRegistration
{
    public static WebApplication MapEndpoints(
        this WebApplication app,
        ServerEnvironment environment,
        ServiceConfig serviceConfig,
        IdentityConfig identityConfig)
    {
        // Root endpoints (existing from Program.cs)
        app.MapRootEndpoints(environment, serviceConfig, identityConfig);
        app.MapErrorEndpoints();
        app.MapWellKnownEndpoints();

        // OAuth endpoints (no shared group)
        app.MapOAuthTokenEndpoints();
        app.MapOAuthAuthorizeEndpoints();
        app.MapOAuthClientMetadataEndpoints();

        // XRPC endpoints — single shared group
        var xrpc = app.MapGroup("xrpc");
        xrpc.MapHealthEndpoints();
        xrpc.MapAppViewProxyEndpoints();

        // Admin
        var admin = xrpc.MapGroup("").WithTags("Admin");
        admin.MapAccountInvitesAdminEndpoints();
        admin.MapAdminDeleteAccountEndpoints();
        admin.MapDisableInviteCodesAdminEndpoints();
        admin.MapGetAccountInfoEndpoints();
        admin.MapGetAccountInfosEndpoints();
        admin.MapGetInviteCodesAdminEndpoints();
        admin.MapSendEmailAdminEndpoints();
        admin.MapSubjectStatusEndpoints();
        admin.MapUpdateAccountEmailAdminEndpoints();
        admin.MapUpdateAccountHandleAdminEndpoints();
        admin.MapUpdateAccountPasswordAdminEndpoints();

        // Identity
        var identity = xrpc.MapGroup("").WithTags("Identity");
        identity.MapResolveHandleEndpoints();
        identity.MapUpdateHandleEndpoints();
        identity.MapGetRecommendedDidCredentialsEndpoints();
        identity.MapRequestPlcOperationSignatureEndpoints();
        identity.MapSignPlcOperationEndpoints();
        identity.MapSubmitPlcOperationEndpoints();

        // Moderation
        xrpc.MapCreateReportEndpoints();

        // Repo
        var repo = xrpc.MapGroup("").WithTags("Repo");
        repo.MapApplyWritesEndpoints();
        repo.MapListRecordsEndpoints();
        repo.MapDescribeRepoEndpoints();
        repo.MapBlobEndpoints();
        repo.MapImportRepoEndpoints();
        repo.MapListMissingBlobsEndpoints();

        // Server
        var server = xrpc.MapGroup("").WithTags("Server");
        server.MapCreateAccountEndpoints();
        server.MapCreateSessionEndpoints();
        server.MapGetSessionEndpoints();
        server.MapRefreshSessionEndpoints();
        server.MapDeleteSessionEndpoints();
        server.MapDeleteAccountEndpoints();
        server.MapDescribeServerEndpoints();
        server.MapCreateInviteCodeEndpoints();
        server.MapCreateInviteCodesEndpoints();
        server.MapGetAccountInviteCodesEndpoints();
        server.MapCheckAccountStatusEndpoints();
        server.MapActivateAccountEndpoints();
        server.MapDeactivateAccountEndpoints();
        server.MapCreateAppPasswordEndpoints();
        server.MapListAppPasswordsEndpoints();
        server.MapRevokeAppPasswordEndpoints();
        server.MapUpdateEmailEndpoints();
        server.MapConfirmEmailEndpoints();
        server.MapRequestEmailConfirmationEndpoints();
        server.MapRequestEmailUpdateEndpoints();
        server.MapRequestPasswordResetEndpoints();
        server.MapResetPasswordEndpoints();
        server.MapReserveSigningKeyEndpoints();
        server.MapGetServiceAuthEndpoints();

        // Sync
        var sync = xrpc.MapGroup("").WithTags("Sync");
        sync.MapSubscribeReposEndpoints();
        sync.MapListReposEndpoints();
        sync.MapGetRepoEndpoints();
        sync.MapGetBlocksEndpoints();
        sync.MapGetBlobEndpoints();
        sync.MapListBlobsEndpoints();
        sync.MapGetRecordEndpoints();
        sync.MapGetLatestCommitEndpoints();
        sync.MapGetRepoStatusEndpoints();

        // Temp
        xrpc.MapCheckSignupQueueEndpoints();

        return app;
    }
}
```

---

## 8. Files to Create / Modify / Delete

### 8.1 New Files (~55 endpoint files + 1 registration file)

| File | Source Controllers |
|---|---|
| `Endpoints/EndpointRegistration.cs` | — (new orchestration) |
| `Endpoints/RootEndpoints.cs` | Inline in `Program.cs` (3 endpoints) |
| `Endpoints/ErrorEndpoints.cs` | `ErrorController.cs` |
| `Endpoints/WellKnownEndpoints.cs` | `WellKnownController.cs` |
| `Endpoints/OAuth/OAuthTokenEndpoints.cs` | `OAuthTokenController.cs` |
| `Endpoints/OAuth/OAuthAuthorizeEndpoints.cs` | `OAuthAuthorizeController.cs` |
| `Endpoints/OAuth/OAuthClientMetadataEndpoints.cs` | `OAuthClientRegistrationController.cs` |
| `Endpoints/Xrpc/HealthEndpoints.cs` | `HealthController.cs` |
| `Endpoints/Xrpc/AppView/AppViewProxyEndpoints.cs` | `AppViewProxyController.cs` |
| `Endpoints/Xrpc/Com/Atproto/Admin/*.cs` (11 files) | 11 admin controllers |
| `Endpoints/Xrpc/Com/Atproto/Identity/*.cs` (6 files) | 6 identity controllers |
| `Endpoints/Xrpc/Com/Atproto/Moderation/CreateReportEndpoints.cs` | `CreateReportController.cs` |
| `Endpoints/Xrpc/Com/Atproto/Repo/*.cs` (6 files) | 6 repo controllers |
| `Endpoints/Xrpc/Com/Atproto/Server/*.cs` (20 files) | 20 server controllers |
| `Endpoints/Xrpc/Com/Atproto/Sync/*.cs` (9 files) | 9 sync controllers |
| `Endpoints/Xrpc/Com/Atproto/Temp/CheckSignupQueueEndpoints.cs` | `CheckSignupQueueController.cs` |

### 8.2 Modified Files

| File | Change |
|---|---|
| `src/atompds/Program.cs` | Remove `AddControllers()`, `MapControllers()`, inline root endpoints. Add `app.MapEndpoints(...)`. |
| `src/atompds/atompds.csproj` | May be able to remove `Microsoft.AspNetCore.Mvc` reference if no other MVC features remain (check after migration). |
| `src/atompds/Middleware/AuthMiddleware.cs` | No changes needed — metadata-based auth works with both MVC and Minimal APIs. |

### 8.3 Deleted Files

All 65 files under `src/atompds/Controllers/` will be deleted after their logic is migrated. The entire `Controllers/` directory will be removed.

---

## 9. Execution Order (Phased)

### Phase 0: Infrastructure (1 file)
1. Create `Endpoints/EndpointRegistration.cs` with the `MapEndpoints` orchestration method.
2. Create `Endpoints/RootEndpoints.cs` — move the 3 inline endpoints from `Program.cs`.
3. Update `Program.cs` — remove `AddControllers()`, `MapControllers()`, inline endpoints; add `app.MapEndpoints(...)`.
4. **Build and test:** Solution must compile. The 3 root endpoints must still work.

### Phase 1: Simple Controllers — No Auth (10 endpoints, ~10 files)
Easy wins to validate the pattern. All use `[Route("xrpc")]`, no auth attributes.

| Endpoint | Controller |
|---|---|
| `_health` | `HealthController` |
| `com.atproto.server.describeServer` | `DescribeServerController` |
| `com.atproto.repo.listRecords` | `ListRecordsController` |
| `com.atproto.repo.describeRepo` | `DescribeRepoController` |
| `com.atproto.sync.listRepos` | `ListReposController` |
| `com.atproto.sync.getRepo` | `GetRepoController` |
| `com.atproto.sync.getBlocks` | `GetBlocksController` |
| `com.atproto.sync.getBlob` | `GetBlobController` |
| `com.atproto.sync.listBlobs` | `ListBlobsController` |
| `com.atproto.sync.getRecord` | `GetRecordController` (Sync) |
| `com.atproto.sync.getLatestCommit` | `GetLatestCommitController` |
| `com.atproto.sync.getRepoStatus` | `GetRepoStatusController` |
| `com.atproto.identity.resolveHandle` | `ResolveHandleController` |
| `com.atproto.repo.listMissingBlobs` | `ListMissingBlobsController` |
| `com.atproto.repo.importRepo` | `ImportRepoController` |

**Build and test** after this phase.

### Phase 2: Auth-Protected Controllers — Single Auth Attribute (30 endpoints, ~30 files)
Controllers using exactly one auth attribute (`[AdminToken]`, `[AccessStandard]`, `[AccessFull]`, `[AccessPrivileged]`, `[Refresh]`) with no rate limiting.

| Pattern | Count | Examples |
|---|---|---|
| `[AdminToken]` | 10 | `AccountInvitesAdmin`, `AdminDeleteAccount`, `GetAccountInfo`, ... |
| `[AccessStandard]` | ~12 | `GetSession`, `ListAppPasswords`, `ConfirmEmail`, ... |
| `[AccessPrivileged]` | ~10 | `UpdateHandle`, `CreateAppPassword`, `UpdateEmail`, ... |
| `[Refresh]` | 2 | `DeleteSession`, ... |
| `[AccessFull]` | 3 | `ActivateAccount`, `DeactivateAccount`, ... |

**Build and test** after this phase.

### Phase 3: Rate-Limited Endpoints (13 endpoints, within existing files)
Add `.RequireRateLimiting("auth-sensitive")` or `.RequireRateLimiting("repo-write")` to endpoints that currently use `[EnableRateLimiting(...)]`.

| Rate Limit Policy | Endpoints |
|---|---|
| `"auth-sensitive"` | `createAccount`, `createSession`, `refreshSession`, `requestPasswordReset`, `resetPassword`, `reserveSigningKey`, `oauth/token` |
| `"repo-write"` | `createRecord`, `putRecord`, `deleteRecord`, `applyWrites`, `uploadBlob` |

**Build and test** after this phase.

### Phase 4: Complex Controllers (5 endpoints, ~5 files)
Controllers with special patterns that need careful conversion.

| Endpoint | Complexity |
|---|---|
| `SubscribeReposController` | WebSocket streaming, CBOR frames |
| `BlobController.UploadBlob` | File upload via `Request.Body` |
| `AppViewProxyController` | 782 lines, catchall routes, reverse proxy |
| `CreateAccountController` | 380 lines, 16 dependencies, captcha, entryway relay |
| `ApplyWritesController` | 315 lines, 13 dependencies, CarpaNet deserialization |

**Build and test** after this phase.

### Phase 5: Non-XRPC Controllers (4 files)
| Endpoint | Controller |
|---|---|
| `/error` | `ErrorController` |
| `.well-known/*` | `WellKnownController` |
| `oauth/token` | `OAuthTokenController` |
| `oauth/authorize*` | `OAuthAuthorizeController` |
| `oauth/client-metadata.json` | `OAuthClientRegistrationController` |

**Build and test** after this phase.

### Phase 6: Cleanup
1. Delete the entire `src/atompds/Controllers/` directory.
2. Remove `builder.Services.AddControllers()` if not already done.
3. Remove `app.MapControllers()` if not already done.
4. Check if `Microsoft.AspNetCore.Mvc` can be removed from `atompds.csproj` — **it likely cannot** because `AuthMiddleware` attributes still use `Microsoft.AspNetCore.Mvc` types indirectly. Verify.
5. Update `AGENTS.md` to reflect the new endpoint-based architecture.
6. Run `dotnet build atompds.slnx` and `dotnet test atompds.slnx`.

---

## 10. Testing Strategy

### Per-Phase Validation
After each phase:
```bash
dotnet build atompds.slnx
dotnet test atompds.slnx
```

### Behavioral Equivalence Checks
For each converted endpoint, verify:

1. **Route matches** — same URL, same HTTP method.
2. **Auth metadata** — same attribute is applied via `.WithMetadata()`.
3. **Rate limiting** — same policy applied via `.RequireRateLimiting()`.
4. **Response shape** — same JSON output (property names, casing, null handling).
5. **Status codes** — same HTTP status codes for success and error cases.
6. **Error handling** — `XRPCError` exceptions still caught by `XRPCExceptionHandler`.

### Regression Risks
| Risk | Mitigation |
|---|---|
| Model binding differences | Minimal APIs use parameter binding, not `[FromBody]`/`[FromQuery]`. Verify query/body binding for each endpoint. |
| JSON serialization | Use `ConfigureHttpJsonOptions` with same `JsonIgnoreCondition`. |
| `HttpContext.Items["AuthOutput"]` | Same mechanism — `AuthMiddleware` runs before endpoint regardless of registration style. |
| `[DisableRateLimiting]` on health | Use `.DisableRateLimiting()` on the health endpoint. |
| Anonymous objects in responses | Minimal APIs serialize anonymous objects the same way via `Results.Json()`. |

---

## 11. Dependency Injection Notes

### Constructor → Method Parameter Migration

MVC controllers use constructor injection. Minimal APIs use method parameter injection. All registered services are resolvable as parameters.

**Before:**
```csharp
public class GetSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    public GetSessionController(AccountRepository accountRepository, ...)
    {
        _accountRepository = accountRepository;
    }
}
```

**After:**
```csharp
public static async Task<IResult> Handle(
    AccountRepository accountRepository, // DI resolves this
    HttpContext context)
```

### High-Dependency Endpoints

`CreateAccountController` (16 deps), `ApplyWritesController` (13 deps), `AppViewProxyController` (7 deps) have many injected services. Consider extracting a handler class for these to keep the endpoint method signature manageable:

```csharp
public sealed class CreateAccountHandler(
    AccountRepository accountRepository,
    ActorRepositoryProvider actorRepositoryProvider,
    // ... 14 more
)
{
    public async Task<IResult> Handle(CreateAccountRequest request, HttpContext context)
    {
        // logic here
    }
}

// Registration
group.MapPost("com.atproto.server.createAccount",
    (CreateAccountRequest request, HttpContext context, CreateAccountHandler handler)
        => handler.Handle(request, context));
```

This is optional — method parameter injection supports unlimited parameters.

---

## 12. Files Not Changed

These files require **no modifications**:

| File | Reason |
|---|---|
| `src/atompds/Middleware/AuthMiddleware.cs` | Reads endpoint metadata; works with both MVC and Minimal APIs |
| `src/atompds/Middleware/AuthMiddleware.cs` (auth attributes) | Plain `Attribute` subclasses; used as metadata |
| `src/atompds/Middleware/RateLimitMiddleware.cs` | Rate limiting policies; `.RequireRateLimiting()` uses same infrastructure |
| `src/atompds/Middleware/NotFoundMiddleware.cs` | Independent middleware |
| `src/atompds/ExceptionHandler/XRPCExceptionHandler.cs` | `IExceptionHandler` — independent of endpoint style |
| `src/atompds/Config/ServerConfig.cs` | DI registration unchanged |
| `src/atompds/Config/ServerEnvironment.cs` | Configuration model unchanged |
| `src/pds_projects/**` | Service layer untouched |
| `src/projects/**` | Lower-level libraries untouched |

---

## 13. Estimated Effort

| Phase | Files | Lines Changed | Complexity |
|---|---|---|---|
| Phase 0: Infrastructure | 3 | ~100 | Low |
| Phase 1: No-auth endpoints | 15 | ~900 | Low |
| Phase 2: Auth-protected | 30 | ~2,000 | Medium |
| Phase 3: Rate-limited | 0 (within Phase 2) | ~20 | Low |
| Phase 4: Complex | 5 | ~1,700 | High |
| Phase 5: Non-XRPC | 4 | ~350 | Medium |
| Phase 6: Cleanup | 65 deleted | — | Low |
| **Total** | ~55 new, 65 deleted, 2 modified | ~5,100 | — |

---

## 14. Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Model binding differences between MVC and Minimal APIs | Medium | Test every endpoint manually; pay attention to `[FromBody]` vs `[FromQuery]` parameter sources |
| Anonymous type JSON serialization differences | Low | Same `System.Text.Json` under the hood; `ConfigureHttpJsonOptions` replicates settings |
| Large controllers (AppViewProxy 782 lines) | Medium | Keep logic in handler class; split into focused private methods |
| Auth middleware compatibility | Low | Already uses endpoint metadata — compatible by design |
| Test coverage gaps | Medium | Add integration tests for converted endpoints before deleting controllers |

---

## 15. Naming Conventions

- **File naming:** `{FeatureName}Endpoints.cs` — plural, PascalCase
- **Class naming:** `static class {FeatureName}Endpoints`
- **Method naming:** `static async Task<IResult> Handle(...)` for single-action endpoints
- **Registration method naming:** `public static RouteGroupBuilder Map{FeatureName}Endpoints(this RouteGroupBuilder group)`
- **Namespace:** Match folder structure under `atompds.Endpoints`

---

## 16. Example: Complete Conversion of `GetSessionController`

### Before
```csharp
// Controllers/Xrpc/Com/Atproto/Server/GetSessionController.cs
[ApiController]
[Route("xrpc")]
public class GetSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<GetSessionController> _logger;

    public GetSessionController(AccountRepository accountRepository, ILogger<GetSessionController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpGet("com.atproto.server.getSession")]
    [AccessStandard]
    public async Task<IActionResult> GetSessionAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        var (active, status) = AccountStore.FormatAccountStatus(account);
        return Ok(new { did, handle = account.Handle, email = account.Email,
            emailConfirmed = account.EmailConfirmedAt != null, active,
            status = status.ToString().ToLowerInvariant() });
    }
}
```

### After
```csharp
// Endpoints/Xrpc/Com/Atproto/Server/GetSessionEndpoints.cs
namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class GetSessionEndpoints
{
    public static RouteGroupBuilder MapGetSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.server.getSession", Handle)
            .WithMetadata(new AccessStandardAttribute());

        return group;
    }

    public static async Task<IResult> Handle(
        HttpContext context,
        AccountRepository accountRepository)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        var (active, status) = AccountStore.FormatAccountStatus(account);
        return Results.Ok(new
        {
            did,
            handle = account.Handle,
            email = account.Email,
            emailConfirmed = account.EmailConfirmedAt != null,
            active,
            status = status.ToString().ToLowerInvariant()
        });
    }
}
```
