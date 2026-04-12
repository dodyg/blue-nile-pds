using AccountManager;
using AccountManager.Db;
using Mailer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class RequestPasswordResetController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly IMailer _mailer;
    private readonly ILogger<RequestPasswordResetController> _logger;

    public RequestPasswordResetController(
        AccountRepository accountRepository,
        IMailer mailer,
        ILogger<RequestPasswordResetController> logger)
    {
        _accountRepository = accountRepository;
        _mailer = mailer;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.requestPasswordReset")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> RequestPasswordResetAsync([FromBody] RequestPasswordResetInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("email is required"));
        }

        var account = await _accountRepository.GetAccountByEmailAsync(request.Email);
        if (account == null)
        {
            return Ok();
        }

        var token = await _accountRepository.CreateEmailTokenAsync(account.Did, EmailToken.EmailTokenPurpose.reset_password);
        await _mailer.SendPasswordResetAsync(token, request.Email);

        return Ok();
    }
}

public class RequestPasswordResetInput
{
    public string? Email { get; set; }
}
