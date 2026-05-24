using AccountManager.Db;
using atompds.Middleware;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class DisableInviteCodesAdminEndpoints
{
    public static RouteGroupBuilder MapDisableInviteCodesAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.disableInviteCodes", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(DisableInviteCodesInput? request, InviteStore inviteStore)
    {
        await inviteStore.DisableInviteCodesAsync(
            request?.Codes ?? [],
            request?.Accounts ?? []);
        return Results.Ok();
    }
}

public class DisableInviteCodesInput
{
    public List<string> Codes { get; set; } = [];
    public List<string> Accounts { get; set; } = [];
}
