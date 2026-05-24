using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class RevokeAppPasswordEndpoints
{
    public static RouteGroupBuilder MapRevokeAppPasswordEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.revokeAppPassword", HandleAsync).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        RevokeAppPasswordInput request,
        AccountRepository accountRepository,
        AppPasswordStore appPasswordStore)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new XRPCError(new InvalidRequestErrorDetail("name is required"));

        var deleted = await appPasswordStore.DeleteAppPasswordAsync(did, request.Name);
        if (!deleted)
            throw new XRPCError(new InvalidRequestErrorDetail("App password not found"));

        await accountRepository.RevokeAppPasswordRefreshTokensAsync(did, request.Name);

        return Results.Ok();
    }
}

public class RevokeAppPasswordInput
{
    public string? Name { get; set; }
}
