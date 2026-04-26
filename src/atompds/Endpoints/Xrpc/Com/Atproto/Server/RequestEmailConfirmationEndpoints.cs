using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Services;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class RequestEmailConfirmationEndpoints
{
    public static RouteGroupBuilder MapRequestEmailConfirmationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.requestEmailConfirmation", HandleAsync).WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        BackgroundEmailDispatcher mailer)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        if (account.Email == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account has no email"));

        var token = await accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.confirm_email);
        await mailer.SendEmailConfirmationAsync(token, account.Email);

        return Results.Ok();
    }
}
