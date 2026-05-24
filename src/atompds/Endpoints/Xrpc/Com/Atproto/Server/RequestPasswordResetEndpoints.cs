using AccountManager;
using AccountManager.Db;
using atompds.Services;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class RequestPasswordResetEndpoints
{
    public static RouteGroupBuilder MapRequestPasswordResetEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.requestPasswordReset", HandleAsync).RequireRateLimiting("auth-sensitive");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        RequestPasswordResetInput request,
        AccountRepository accountRepository,
        BackgroundEmailDispatcher mailer)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new XRPCError(new InvalidRequestErrorDetail("email is required"));

        var account = await accountRepository.GetAccountByEmailAsync(request.Email);
        if (account == null)
            return Results.Ok();

        var token = await accountRepository.CreateEmailTokenAsync(account.Did, EmailToken.EmailTokenPurpose.reset_password);
        await mailer.SendPasswordResetAsync(token, request.Email);

        return Results.Ok();
    }
}

public class RequestPasswordResetInput
{
    public string? Email { get; set; }
}
