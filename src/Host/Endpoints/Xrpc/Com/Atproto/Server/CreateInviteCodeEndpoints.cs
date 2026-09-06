using BlueNilePds.Pds.AccountManager;
using BlueNilePds.Pds.AccountManager.Db;
using BlueNilePds.Host.Middleware;
using ComAtproto.Server;
using BlueNilePds.Pds.Xrpc;

namespace BlueNilePds.Host.Endpoints.Xrpc.Com.Atproto.Server;

public static class CreateInviteCodeEndpoints
{
    public static RouteGroupBuilder MapCreateInviteCodeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.createInviteCode", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        CreateInviteCodeInput request,
        InviteStore inviteStore,
        AccountRepository accountRepository)
    {
        var useCount = request.UseCount > 0 ? request.UseCount : 1;
        var forAccount = request.ForAccount is not null ? (string)request.ForAccount : throw new XRPCError(new InvalidRequestErrorDetail("forAccount is required"));

        var account = await accountRepository.GetAccountAsync(forAccount, new AvailabilityFlags(IncludeTakenDown: true, IncludeDeactivated: true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        if (account.InvitesDisabled == true)
            throw new XRPCError(new InvalidRequestErrorDetail("Invites are disabled for this account"));

        var code = await inviteStore.CreateInviteCodeAsync(forAccount, forAccount, (int)useCount);

        return Results.Ok(new CreateInviteCodeOutput { Code = code });
    }
}
