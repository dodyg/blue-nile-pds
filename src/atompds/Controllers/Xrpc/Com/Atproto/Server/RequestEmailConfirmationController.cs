using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Services;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class RequestEmailConfirmationController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly BackgroundEmailDispatcher _mailer;
    private readonly ILogger<RequestEmailConfirmationController> _logger;

    public RequestEmailConfirmationController(
        AccountRepository accountRepository,
        BackgroundEmailDispatcher mailer,
        ILogger<RequestEmailConfirmationController> logger)
    {
        _accountRepository = accountRepository;
        _mailer = mailer;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.requestEmailConfirmation")]
    [AccessStandard]
    public async Task<IActionResult> RequestEmailConfirmationAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        if (account.Email == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account has no email"));
        }

        var token = await _accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.confirm_email);
        await _mailer.SendEmailConfirmationAsync(token, account.Email);

        return Ok();
    }
}
