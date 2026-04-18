using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Mailer;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class RequestEmailConfirmationController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly IMailer _mailer;

    public RequestEmailConfirmationController(AccountRepository accountRepository, IMailer mailer)
    {
        _accountRepository = accountRepository;
        _mailer = mailer;
    }

    [HttpPost("com.atproto.server.requestEmailConfirmation")]
    [AccessFull(true)]
    public async Task<IActionResult> RequestEmailConfirmationAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found."));
        }

        if (string.IsNullOrWhiteSpace(account.Email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account does not have an email address."));
        }

        var token = await _accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.confirm_email);
        await _mailer.SendConfirmEmailAsync(token, account.Email);
        return NoContent();
    }
}
