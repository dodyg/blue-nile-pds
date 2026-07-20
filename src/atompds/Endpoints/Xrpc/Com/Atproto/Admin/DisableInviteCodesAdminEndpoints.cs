using AccountManager.Db;
using atompds.Middleware;
using ComAtproto.Admin;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class DisableInviteCodesAdminEndpoints
{
    public static RouteGroupBuilder MapDisableInviteCodesAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.disableInviteCodes", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(ComAtproto.Admin.DisableInviteCodesInput? request, InviteStore inviteStore)
    {
        await inviteStore.DisableInviteCodesAsync(
            request?.Codes ?? [],
            request?.Accounts ?? []);
        return Results.Ok(new { });
    }
}
