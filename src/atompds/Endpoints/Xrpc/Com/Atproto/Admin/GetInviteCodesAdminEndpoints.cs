using AccountManager.Db;
using atompds.Middleware;
using CarpaNet;
using ComAtproto.Admin;
using ComAtproto.Server;

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

        return Results.Ok(new GetInviteCodesOutput
        {
            Cursor = nextCursor,
            Codes = codes.Select(code =>
            {
                var codeUses = uses.GetValueOrDefault(code.Code, []);
                return new DefsInviteCode
                {
                    Code = code.Code,
                    Available = code.AvailableUses,
                    Disabled = code.Disabled,
                    ForAccount = code.ForAccount,
                    CreatedBy = code.CreatedBy,
                    CreatedAt = new DateTimeOffset(code.CreatedAt, TimeSpan.Zero),
                    Uses = codeUses.Select(u => new DefsInviteCodeUse
                    {
                        UsedBy = new ATDid(u.UsedBy),
                        UsedAt = new DateTimeOffset(u.UsedAt, TimeSpan.Zero)
                    }).ToList()
                };
            }).ToList()
        });
    }
}
