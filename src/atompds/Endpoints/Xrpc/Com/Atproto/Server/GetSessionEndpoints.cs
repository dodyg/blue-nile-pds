using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class GetSessionEndpoints
{
    public static RouteGroupBuilder MapGetSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.server.getSession", HandleAsync).WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        var (active, status) = AccountStore.FormatAccountStatus(account);

        return Results.Ok(new
        {
            did,
            handle = account.Handle,
            email = account.Email,
            emailConfirmed = account.EmailConfirmedAt != null,
            active,
            status = status.ToString().ToLowerInvariant()
        });
    }
}
