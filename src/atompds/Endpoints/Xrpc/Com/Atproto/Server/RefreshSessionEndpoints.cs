using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Utils;
using CarpaNet;
using CommonWeb;
using ComAtproto.Server;
using Config;
using Identity;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class RefreshSessionEndpoints
{
    public static RouteGroupBuilder MapRefreshSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.refreshSession", HandleAsync)
            .WithMetadata(new RefreshAttribute())
            .RequireRateLimiting("auth-sensitive");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        IdentityConfig identityConfig,
        IdResolver idResolver,
        ILogger<Program> logger)
    {
        var auth = context.GetRefreshOutput();
        var did = auth.RefreshCredentials.Did;
        var tokenId = auth.RefreshCredentials.TokenId;

        var user = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (user == null)
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not find user info for account: {did}"));

        if (user.SoftDeleted)
            throw new XRPCError(new AccountTakenDownErrorDetail("Account has been taken down"));

        var didDocTask = DidDocForSessionAsync(did, identityConfig, idResolver, logger);
        var rotateTask = accountRepository.RotateRefreshTokenAsync(tokenId);
        await Task.WhenAll(didDocTask, rotateTask);

        var didDoc = didDocTask.Result;
        var rotated = rotateTask.Result;

        if (rotated == null)
            throw new XRPCError(new ExpiredTokenErrorDetail("Token has been revoked"));

        var (active, status) = AccountStore.FormatAccountStatus(user);

        return Results.Ok(new RefreshSessionOutput
        {
            AccessJwt = rotated.Value.AccessJwt,
            RefreshJwt = rotated.Value.RefreshJwt,
            Handle = new ATHandle(user.Handle ?? Constants.INVALID_HANDLE),
            Did = new ATDid(user.Did),
            DidDoc = didDoc?.ToJsonElement(),
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
}
