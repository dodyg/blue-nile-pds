using AccountManager;
using AccountManager.Db;
using atompds.Middleware;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class GetAccountInviteCodesEndpoints
{
    public static RouteGroupBuilder MapGetAccountInviteCodesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.server.getAccountInviteCodes", HandleAsync).WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        InviteStore inviteStore)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var codes = await inviteStore.GetInviteCodesForAccountAsync(did);

        return Results.Ok(new
        {
            codes = codes.Select(c => new
            {
                code = c.Code,
                available = c.AvailableUses,
                disabled = c.Disabled,
                forAccount = c.ForAccount,
                createdBy = c.CreatedBy,
                createdAt = c.CreatedAt.ToString("o")
            })
        });
    }
}
