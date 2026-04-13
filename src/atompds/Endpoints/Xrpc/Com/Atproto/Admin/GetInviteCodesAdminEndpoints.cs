using AccountManager.Db;
using atompds.Middleware;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class GetInviteCodesAdminEndpoints
{
    public static RouteGroupBuilder MapGetInviteCodesAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.admin.getInviteCodes", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        InviteStore inviteStore,
        string sort = "recent",
        int limit = 100,
        string? cursor = null)
    {
        if (limit < 1 || limit > 500)
            limit = 100;

        var (codes, nextCursor) = await inviteStore.GetInviteCodesAsync(sort, limit, cursor);
        var uses = await inviteStore.GetInviteCodeUsesAsync(codes.Select(code => code.Code));

        return Results.Ok(new
        {
            cursor = nextCursor,
            codes = codes.Select(code => new
            {
                code = code.Code,
                available = code.AvailableUses,
                disabled = code.Disabled,
                forAccount = code.ForAccount,
                createdBy = code.CreatedBy,
                createdAt = code.CreatedAt.ToString("o"),
                uses = uses.GetValueOrDefault(code.Code, [])
                    .Select(use => new
                    {
                        usedBy = use.UsedBy,
                        usedAt = use.UsedAt.ToString("o")
                    })
            })
        });
    }
}
