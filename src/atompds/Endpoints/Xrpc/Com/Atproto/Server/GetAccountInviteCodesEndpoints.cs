using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using CarpaNet;
using ComAtproto.Server;

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
        var uses = await inviteStore.GetInviteCodeUsesAsync(codes.Select(c => c.Code));

        return Results.Ok(new GetAccountInviteCodesOutput
        {
            Codes = codes.Select(c =>
            {
                var codeUses = uses.GetValueOrDefault(c.Code, []);
                return new DefsInviteCode
                {
                    Code = c.Code,
                    Available = c.AvailableUses,
                    Disabled = c.Disabled,
                    ForAccount = c.ForAccount,
                    CreatedBy = c.CreatedBy,
                    CreatedAt = new DateTimeOffset(c.CreatedAt, TimeSpan.Zero),
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
