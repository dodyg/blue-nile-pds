using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Services;
using Mailer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class RequestEmailUpdateEndpoints
{
    public static RouteGroupBuilder MapRequestEmailUpdateEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.requestEmailUpdate", HandleAsync).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        IMailer mailer,
        EntrywayRelayService entrywayRelayService)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        if (entrywayRelayService.IsConfigured)
        {
            return await entrywayRelayService.ForwardWithoutBodyAsync(
                context.Request,
                HttpMethod.Post,
                "/xrpc/com.atproto.server.requestEmailUpdate",
                did,
                "com.atproto.server.requestEmailUpdate",
                context.RequestAborted);
        }

        if (string.IsNullOrWhiteSpace(account.Email))
            throw new XRPCError(new InvalidRequestErrorDetail("Account does not have an email address"));

        var tokenRequired = account.EmailConfirmedAt != null;
        if (tokenRequired)
        {
            var token = await accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.update_email);
            await mailer.SendEmailUpdateAsync(token, account.Email);
        }

        return Results.Ok(new RequestEmailUpdateOutput { TokenRequired = tokenRequired });
    }
}

public class RequestEmailUpdateOutput
{
    public bool TokenRequired { get; set; }
}
