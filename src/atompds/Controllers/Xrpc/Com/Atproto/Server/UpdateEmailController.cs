using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class UpdateEmailController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<UpdateEmailController> _logger;

    public UpdateEmailController(AccountRepository accountRepository, ILogger<UpdateEmailController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.updateEmail")]
    [AccessPrivileged]
    public async Task<IActionResult> UpdateEmailAsync([FromBody] UpdateEmailInput request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("email is required"));
        }

        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            await _accountRepository.AssertValidEmailTokenAsync(did, request.Token, EmailToken.EmailTokenPurpose.update_email);
        }

        await _accountRepository.UpdateEmailAsync(did, request.Email);

        return Ok();
    }
}

public class UpdateEmailInput
{
    public string? Email { get; set; }
    public string? Token { get; set; }
}
