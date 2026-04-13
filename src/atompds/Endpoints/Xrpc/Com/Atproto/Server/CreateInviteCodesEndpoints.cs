using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class CreateInviteCodesEndpoints
{
    public static RouteGroupBuilder MapCreateInviteCodesEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.createInviteCodes", HandleAsync).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        CreateInviteCodesInput request,
        AccountRepository accountRepository,
        InviteStore inviteStore)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await accountRepository.GetAccountAsync(did);
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        if (account.InvitesDisabled == true)
            throw new XRPCError(new InvalidRequestErrorDetail("Invites are disabled for this account"));

        var useCount = request.UseCount > 0 ? request.UseCount : 1;
        var codes = new List<string>();
        for (var i = 0; i < request.CodeCount; i++)
        {
            var code = await inviteStore.CreateInviteCodeAsync(did, did, useCount);
            codes.Add(code);
        }

        return Results.Ok(new { codes });
    }
}

public class CreateInviteCodesInput
{
    public int CodeCount { get; set; } = 1;
    public int UseCount { get; set; } = 1;
}
