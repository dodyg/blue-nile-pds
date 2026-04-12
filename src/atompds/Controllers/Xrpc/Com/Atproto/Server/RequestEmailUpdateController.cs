using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Mailer;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class RequestEmailUpdateController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly IMailer _mailer;
    private readonly ILogger<RequestEmailUpdateController> _logger;

    public RequestEmailUpdateController(
        AccountRepository accountRepository,
        IMailer mailer,
        ILogger<RequestEmailUpdateController> logger)
    {
        _accountRepository = accountRepository;
        _mailer = mailer;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.requestEmailUpdate")]
    [AccessPrivileged]
    public async Task<IActionResult> RequestEmailUpdateAsync([FromBody] RequestEmailUpdateInput request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("email is required"));
        }

        var token = await _accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.update_email);
        await _mailer.SendEmailUpdateAsync(token, request.Email);

        return Ok();
    }
}

public class RequestEmailUpdateInput
{
    public string? Email { get; set; }
}
