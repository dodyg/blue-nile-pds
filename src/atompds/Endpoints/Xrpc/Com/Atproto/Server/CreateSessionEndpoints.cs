using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using atompds.Services;
using atompds.Utils;
using CarpaNet;
using CommonWeb;
using ComAtproto.Server;
using Config;
using Identity;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class CreateSessionEndpoints
{
    public static RouteGroupBuilder MapCreateSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.createSession", HandleAsync).RequireRateLimiting("auth-sensitive");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        JsonElement request,
        AccountRepository accountRepository,
        IdentityConfig identityConfig,
        IdResolver idResolver,
        EntrywayRelayService entrywayRelayService,
        ILogger<Program> logger)
    {
        if (entrywayRelayService.IsConfigured)
        {
            return await entrywayRelayService.ForwardJsonAsync(
                context.Request,
                "/xrpc/com.atproto.server.createSession",
                request.GetRawText(),
                cancellationToken: context.RequestAborted);
        }

        var identifier = TryGetString(request, "identifier");
        var password = TryGetString(request, "password");
        var allowTakendown = TryGetBoolean(request, "allowTakendown") == true;

        if (identifier == null || password == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Identifier and password are required"));

        var login = await accountRepository.LoginAsync(identifier, password, allowTakendown);
        var creds = await accountRepository.CreateSessionAsync(login.Account.Did, login.AppPasswordName, login.AppPasswordScope);
        var didDoc = await DidDocForSessionAsync(login.Account.Did, identityConfig, idResolver, logger);
        var (active, status) = AccountStore.FormatAccountStatus(login.Account);

        return Results.Ok(new CreateSessionOutput
        {
            AccessJwt = creds.AccessJwt,
            RefreshJwt = creds.RefreshJwt,
            Handle = new ATHandle(login.Account.Handle ?? Constants.INVALID_HANDLE),
            Did = new ATDid(login.Account.Did),
            DidDoc = didDoc?.ToJsonElement(),
            Email = login.Account.Email,
            EmailConfirmed = login.Account.EmailConfirmedAt != null,
            EmailAuthFactor = null,
            Active = active,
            Status = status.ToString()
        });
    }

    private static async Task<DidDocument?> DidDocForSessionAsync(string did, IdentityConfig identityConfig, IdResolver idResolver, ILogger logger)
    {
        if (!identityConfig.EnableDidDocWithSession) return null;
        try
        {
            return await idResolver.DidResolver.ResolveAsync(did, false);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to resolve did doc: {did}", did);
            return null;
        }
    }

    private static string? TryGetString(JsonElement body, string propertyName)
    {
        return body.ValueKind == JsonValueKind.Object &&
               body.TryGetProperty(propertyName, out var property) &&
               property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static bool? TryGetBoolean(JsonElement body, string propertyName)
    {
        return body.ValueKind == JsonValueKind.Object &&
               body.TryGetProperty(propertyName, out var property) &&
               (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;
    }
}
