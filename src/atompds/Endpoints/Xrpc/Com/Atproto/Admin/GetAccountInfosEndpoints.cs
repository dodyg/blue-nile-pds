using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using CarpaNet;
using ComAtproto.Admin;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class GetAccountInfosEndpoints
{
    public static RouteGroupBuilder MapGetAccountInfosEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.admin.getAccountInfos", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(string dids, AccountRepository accountRepository)
    {
        var didList = dids.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var accounts = await accountRepository.GetAccountsAsync(didList, new AvailabilityFlags(true, true));

        return Results.Ok(new GetAccountInfosOutput
        {
            Infos = accounts.Values.Select(a => new DefsAccountView
            {
                Did = new ATDid(a.Did),
                Handle = new ATHandle(a.Handle ?? string.Empty),
                Email = a.Email,
                EmailConfirmedAt = a.EmailConfirmedAt.HasValue
                    ? new DateTimeOffset(a.EmailConfirmedAt.Value, TimeSpan.Zero) : null,
                InvitesDisabled = a.InvitesDisabled,
                IndexedAt = new DateTimeOffset(a.CreatedAt, TimeSpan.Zero),
                DeactivatedAt = a.DeactivatedAt.HasValue
                    ? new DateTimeOffset(a.DeactivatedAt.Value, TimeSpan.Zero) : null
            }).ToList()
        });
    }
}
