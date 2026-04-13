using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Mailer;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class ConfirmEmailController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<ConfirmEmailController> _logger;

    public ConfirmEmailController(AccountRepository accountRepository, ILogger<ConfirmEmailController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.confirmEmail")]
    [AccessStandard]
    public async Task<IActionResult> ConfirmEmailAsync([FromBody] ConfirmEmailInput request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("token is required"));
        }

        await _accountRepository.AssertValidEmailTokenAsync(did, request.Token, EmailToken.EmailTokenPurpose.confirm_email);
        await _accountRepository.ConfirmEmailAsync(did);

        return Ok();
    }
}

public class ConfirmEmailInput
{
    public string? Token { get; set; }
}
