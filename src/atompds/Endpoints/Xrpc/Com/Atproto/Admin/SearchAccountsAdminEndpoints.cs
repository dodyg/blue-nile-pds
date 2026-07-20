using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using CarpaNet;
using ComAtproto.Admin;
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
            throw new XRPCError(new InvalidRequestErrorDetail("limit must be between 1 and 100"));

        var (accounts, nextCursor) = await accountStore.SearchAccountsAsync(email, cursor, actualLimit);

        return Results.Ok(new SearchAccountsOutput
        {
            Accounts = accounts.Select(a => new DefsAccountView
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
            }).ToList(),
            Cursor = nextCursor
        });
    }
}
