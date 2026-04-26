using AccountManager;
using atompds.Middleware;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class DeleteSessionEndpoints
{
    public static RouteGroupBuilder MapDeleteSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.deleteSession", HandleAsync).WithMetadata(new RefreshAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository)
    {
        var auth = context.GetRefreshOutput();
        var tokenId = auth.RefreshCredentials.TokenId;
        await accountRepository.RevokeRefreshTokenAsync(tokenId);
        return Results.Ok();
    }
}
