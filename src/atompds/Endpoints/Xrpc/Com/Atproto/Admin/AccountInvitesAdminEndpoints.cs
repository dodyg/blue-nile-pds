using AccountManager;
using atompds.Middleware;
using CarpaNet;
using ComAtproto.Admin;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class AccountInvitesAdminEndpoints
{
    public static RouteGroupBuilder MapAccountInvitesAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.enableAccountInvites", EnableAsync).WithMetadata(new AdminTokenAttribute());
        group.MapPost("com.atproto.admin.disableAccountInvites", DisableAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> EnableAsync(EnableAccountInvitesInput request, AccountRepository accountRepository)
    {
        await accountRepository.UpdateInvitesDisabledAsync(request.Account, false);
        return Results.Ok(new { });
    }

    private static async Task<IResult> DisableAsync(DisableAccountInvitesInput request, AccountRepository accountRepository)
    {
        await accountRepository.UpdateInvitesDisabledAsync(request.Account, true);
        return Results.Ok(new { });
    }
}
