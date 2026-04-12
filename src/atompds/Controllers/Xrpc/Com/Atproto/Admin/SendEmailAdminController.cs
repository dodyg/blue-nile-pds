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
        if (string.IsNullOrWhiteSpace(request.Did) || string.IsNullOrWhiteSpace(request.Content))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did and content are required"));
        }

        var account = await _accountRepository.GetAccountAsync(request.Did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        if (account.Email == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account has no email"));
        }

        var token = await _accountRepository.CreateEmailTokenAsync(account.Did, EmailToken.EmailTokenPurpose.delete_account);
        await _mailer.SendAccountDeleteAsync(token, account.Email);

        return Ok();
    }
}

public class AdminSendEmailInput
{
    public string? Did { get; set; }
    public string? Content { get; set; }
    public string? Subject { get; set; }
}
