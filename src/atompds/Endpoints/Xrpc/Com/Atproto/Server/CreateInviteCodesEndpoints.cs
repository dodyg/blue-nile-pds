using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using ComAtproto.Server;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class CreateInviteCodesEndpoints
{
    public static RouteGroupBuilder MapCreateInviteCodesEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.createInviteCodes", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        CreateInviteCodesInput request,
        InviteStore inviteStore,
        AccountRepository accountRepository)
    {
        var useCount = request.UseCount > 0 ? request.UseCount : 1;
        var codeCount = request.CodeCount > 0 ? request.CodeCount : 1;
        var forAccounts = request.ForAccounts ?? throw new XRPCError(new InvalidRequestErrorDetail("forAccounts is required"));

        var results = new List<CreateInviteCodesAccountCodes>();
        foreach (var forAccount in forAccounts)
        {
            var did = (string)forAccount;
            var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(IncludeTakenDown: true, IncludeDeactivated: true));
            if (account == null)
                throw new XRPCError(new InvalidRequestErrorDetail($"Account not found: {did}"));
            if (account.InvitesDisabled == true)
                throw new XRPCError(new InvalidRequestErrorDetail($"Invites are disabled for account: {did}"));

            var codes = new List<string>();
            for (var i = 0; i < codeCount; i++)
            {
                var code = await inviteStore.CreateInviteCodeAsync(did, did, (int)useCount);
                codes.Add(code);
            }
            results.Add(new CreateInviteCodesAccountCodes
            {
                Account = did,
                Codes = codes
            });
        }

        return Results.Ok(new CreateInviteCodesOutput { Codes = results });
    }
}
