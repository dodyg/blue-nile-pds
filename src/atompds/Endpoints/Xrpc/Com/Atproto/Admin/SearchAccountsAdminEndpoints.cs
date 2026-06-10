using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class SearchAccountsAdminEndpoints
{
    public static RouteGroupBuilder MapSearchAccountsAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.admin.searchAccounts", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(string? email, string? cursor, int? limit, AccountStore accountStore)
    {
        var actualLimit = limit ?? 50;
        if (actualLimit < 1 || actualLimit > 100)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("limit must be between 1 and 100"));
        }

        var (accounts, nextCursor) = await accountStore.SearchAccountsAsync(email, cursor, actualLimit);

        var response = new
        {
            accounts = accounts.Select(a => new
            {
                did = a.Did,
                handle = a.Handle,
                email = a.Email,
                emailConfirmedAt = a.EmailConfirmedAt?.ToString("o"),
                invitesDisabled = a.InvitesDisabled,
                takedownRef = a.TakedownRef,
                deactivatedAt = a.DeactivatedAt?.ToString("o"),
                createdAt = a.CreatedAt.ToString("o")
            }),
            cursor = nextCursor
        };

        return Results.Ok(response);
    }
}
