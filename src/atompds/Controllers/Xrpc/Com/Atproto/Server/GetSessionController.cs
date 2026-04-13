using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class GetSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<GetSessionController> _logger;

    public GetSessionController(AccountRepository accountRepository, ILogger<GetSessionController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpGet("com.atproto.server.getSession")]
    [AccessStandard]
    public async Task<IActionResult> GetSessionAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        var (active, status) = AccountStore.FormatAccountStatus(account);

        return Ok(new
        {
            did,
            handle = account.Handle,
            email = account.Email,
            emailConfirmed = account.EmailConfirmedAt != null,
            active,
            status = status.ToString().ToLowerInvariant()
        });
    }
}
