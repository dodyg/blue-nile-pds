using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class GetAccountInfoController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<GetAccountInfoController> _logger;

    public GetAccountInfoController(AccountRepository accountRepository, ILogger<GetAccountInfoController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpGet("com.atproto.admin.getAccountInfo")]
    [AdminToken]
    public async Task<IActionResult> GetAccountInfoAsync([FromQuery] string did)
    {
        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        return Ok(new
        {
            did = account.Did,
            handle = account.Handle,
            email = account.Email,
            emailConfirmedAt = account.EmailConfirmedAt?.ToString("o"),
            invitesDisabled = account.InvitesDisabled,
            takedownRef = account.TakedownRef,
            deactivatedAt = account.DeactivatedAt?.ToString("o"),
            createdAt = account.CreatedAt.ToString("o")
        });
    }
}
