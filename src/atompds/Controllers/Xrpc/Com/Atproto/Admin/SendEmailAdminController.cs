using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Mailer;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class SendEmailAdminController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly IMailer _mailer;
    private readonly ILogger<SendEmailAdminController> _logger;

    public SendEmailAdminController(AccountRepository accountRepository, IMailer mailer, ILogger<SendEmailAdminController> logger)
    {
        _accountRepository = accountRepository;
        _mailer = mailer;
        _logger = logger;
    }

    [HttpPost("com.atproto.admin.sendEmail")]
    [AdminToken]
    public async Task<IActionResult> SendEmailAsync([FromBody] AdminSendEmailInput request)
    {
        var recipientDid = request.RecipientDid ?? request.Did;
        if (string.IsNullOrWhiteSpace(recipientDid) || string.IsNullOrWhiteSpace(request.Content))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("recipientDid and content are required"));
        }

        var account = await _accountRepository.GetAccountAsync(recipientDid, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Recipient not found"));
        }

        if (account.Email == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account has no email"));
        }

        await _mailer.SendCustomEmailAsync(
            request.Subject ?? "Message via your PDS",
            request.Content,
            account.Email);

        return Ok(new { sent = true });
    }
}

public class AdminSendEmailInput
{
    public string? Did { get; set; }
    public string? RecipientDid { get; set; }
    public string? Content { get; set; }
    public string? Subject { get; set; }
}
