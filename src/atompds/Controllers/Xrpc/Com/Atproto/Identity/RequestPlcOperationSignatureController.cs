using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Mailer;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Identity;

[ApiController]
[Route("xrpc")]
public class RequestPlcOperationSignatureController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<RequestPlcOperationSignatureController> _logger;
    private readonly IMailer _mailer;

    public RequestPlcOperationSignatureController(
        AccountRepository accountRepository,
        IMailer mailer,
        ILogger<RequestPlcOperationSignatureController> logger)
    {
        _accountRepository = accountRepository;
        _mailer = mailer;
        _logger = logger;
    }

    [HttpPost("com.atproto.identity.requestPlcOperationSignature")]
    [AccessPrivileged]
    public async Task<IActionResult> RequestPlcOperationSignatureAsync()
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

        var token = await _accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.plc_operation);
        await _mailer.SendPlcOperationSignatureAsync(token, account.Email);

        return Ok();
    }
}
