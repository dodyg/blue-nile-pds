using AccountManager.Db;
using atompds.Middleware;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class ListAppPasswordsEndpoints
{
    public static RouteGroupBuilder MapListAppPasswordsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.server.listAppPasswords", HandleAsync).WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AppPasswordStore appPasswordStore)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var passwords = await appPasswordStore.ListAppPasswordsAsync(did);

        return Results.Ok(new
        {
            passwords = passwords.Select(ap => new
            {
                name = ap.Name,
                createdAt = ap.CreatedAt.ToString("o"),
                privileged = ap.Privileged
            })
        });
    }
}
