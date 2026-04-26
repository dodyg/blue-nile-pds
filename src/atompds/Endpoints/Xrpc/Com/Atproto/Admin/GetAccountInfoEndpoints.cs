using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class GetAccountInfoEndpoints
{
    public static RouteGroupBuilder MapGetAccountInfoEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.admin.getAccountInfo", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(string? did, AccountRepository accountRepository)
    {
        if (string.IsNullOrWhiteSpace(did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        return Results.Ok(new
        {
            did = account.Did,
            handle = account.Handle,
            email = account.Email,
            emailConfirmedAt = account.EmailConfirmedAt?.ToString("o"),
            invitesDisabled = account.InvitesDisabled,
            takedownRef = account.TakedownRef,
            deactivatedAt = account.DeactivatedAt?.ToString("o"),
            createdAt = account.CreatedAt.ToString("o")
        });
    }
}
