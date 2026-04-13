using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Services;
using Mailer;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class RequestEmailUpdateController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly EntrywayRelayService _entrywayRelayService;
    private readonly IMailer _mailer;
    private readonly ILogger<RequestEmailUpdateController> _logger;

    public RequestEmailUpdateController(
        AccountRepository accountRepository,
        IMailer mailer,
        EntrywayRelayService entrywayRelayService,
        ILogger<RequestEmailUpdateController> logger)
    {
        _accountRepository = accountRepository;
        _mailer = mailer;
        _entrywayRelayService = entrywayRelayService;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.requestEmailUpdate")]
    [AccessPrivileged]
    public async Task<IActionResult> RequestEmailUpdateAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        if (_entrywayRelayService.IsConfigured)
        {
            return await _entrywayRelayService.ForwardWithoutBodyAsync(
                HttpContext.Request,
                HttpMethod.Post,
                "/xrpc/com.atproto.server.requestEmailUpdate",
                did,
                "com.atproto.server.requestEmailUpdate",
                HttpContext.RequestAborted);
        }

        if (string.IsNullOrWhiteSpace(account.Email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account does not have an email address"));
        }

        var tokenRequired = account.EmailConfirmedAt != null;
        if (tokenRequired)
        {
            var token = await _accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.update_email);
            await _mailer.SendEmailUpdateAsync(token, account.Email);
        }

        return Ok(new RequestEmailUpdateOutput
        {
            TokenRequired = tokenRequired
        });
    }
}

public class RequestEmailUpdateOutput
{
    public bool TokenRequired { get; set; }
}
