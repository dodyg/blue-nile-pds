using AccountManager;
using AccountManager.Db;
using atompds.Middleware;

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

        return Results.Ok(new
        {
            accounts = accounts.Values.Select(a => new
            {
                did = a.Did,
                handle = a.Handle,
                email = a.Email,
                emailConfirmedAt = a.EmailConfirmedAt?.ToString("o"),
                takedownRef = a.TakedownRef,
                deactivatedAt = a.DeactivatedAt?.ToString("o"),
                createdAt = a.CreatedAt.ToString("o")
            })
        });
    }
}
