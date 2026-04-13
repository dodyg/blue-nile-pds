using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Mailer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class ConfirmEmailEndpoints
{
    public static RouteGroupBuilder MapConfirmEmailEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.confirmEmail", HandleAsync).WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        ConfirmEmailInput request,
        AccountRepository accountRepository)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new XRPCError(new InvalidRequestErrorDetail("token is required"));

        await accountRepository.AssertValidEmailTokenAsync(did, request.Token, EmailToken.EmailTokenPurpose.confirm_email);
        await accountRepository.ConfirmEmailAsync(did);

        return Results.Ok();
    }
}

public class ConfirmEmailInput
{
    public string? Token { get; set; }
}
