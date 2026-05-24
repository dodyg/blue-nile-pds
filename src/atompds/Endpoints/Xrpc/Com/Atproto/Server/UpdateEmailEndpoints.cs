using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Services;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class UpdateEmailEndpoints
{
    public static RouteGroupBuilder MapUpdateEmailEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.updateEmail", HandleAsync).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UpdateEmailInput request,
        AccountRepository accountRepository,
        EmailAddressValidator emailAddressValidator,
        EntrywayRelayService entrywayRelayService)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new XRPCError(new InvalidRequestErrorDetail("email is required"));

        await emailAddressValidator.AssertSupportedEmailAsync(request.Email);

        if (entrywayRelayService.IsConfigured)
        {
            return await entrywayRelayService.ForwardJsonAsync(
                context.Request,
                "/xrpc/com.atproto.server.updateEmail",
                System.Text.Json.JsonSerializer.Serialize(request, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
                did,
                "com.atproto.server.updateEmail",
                context.RequestAborted);
        }

        var existingAccount = await accountRepository.GetAccountByEmailAsync(request.Email, new AvailabilityFlags(true, true));
        if (existingAccount != null && existingAccount.Did != did)
            throw new XRPCError(new InvalidRequestErrorDetail("This email address is already in use, please use a different email."));

        if (account.EmailConfirmedAt != null)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                throw new XRPCError(new InvalidRequestErrorDetail("TokenRequired", "confirmation token required"));

            await accountRepository.AssertValidEmailTokenAsync(did, request.Token, EmailToken.EmailTokenPurpose.update_email);
        }

        await accountRepository.UpdateEmailAsync(did, request.Email);

        return Results.Ok();
    }
}

public class UpdateEmailInput
{
    public string? Email { get; set; }
    public bool? EmailAuthFactor { get; set; }
    public string? Token { get; set; }
}
