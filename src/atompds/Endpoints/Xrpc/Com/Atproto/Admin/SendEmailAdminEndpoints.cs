using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Services;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class SendEmailAdminEndpoints
{
    public static RouteGroupBuilder MapSendEmailAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.sendEmail", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        AdminSendEmailInput request,
        AccountRepository accountRepository,
        BackgroundEmailDispatcher mailer)
    {
        var recipientDid = request.RecipientDid ?? request.Did;
        if (string.IsNullOrWhiteSpace(recipientDid) || string.IsNullOrWhiteSpace(request.Content))
            throw new XRPCError(new InvalidRequestErrorDetail("recipientDid and content are required"));

        var account = await accountRepository.GetAccountAsync(recipientDid, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Recipient not found"));

        if (account.Email == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account has no email"));

        await mailer.SendCustomEmailAsync(
            request.Subject ?? "Message via your PDS",
            request.Content,
            account.Email);

        return Results.Ok(new { sent = true });
    }
}

public class AdminSendEmailInput
{
    public string? Did { get; set; }
    public string? RecipientDid { get; set; }
    public string? Content { get; set; }
    public string? Subject { get; set; }
}
