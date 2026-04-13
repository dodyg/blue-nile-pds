using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class AccountInvitesAdminController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<AccountInvitesAdminController> _logger;

    public AccountInvitesAdminController(AccountRepository accountRepository, ILogger<AccountInvitesAdminController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpPost("com.atproto.admin.enableAccountInvites")]
    [AdminToken]
    public async Task<IActionResult> EnableAccountInvitesAsync([FromBody] EnableInvitesInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        }

        await _accountRepository.UpdateInvitesDisabledAsync(request.Did, false);
        return Ok();
    }

    [HttpPost("com.atproto.admin.disableAccountInvites")]
    [AdminToken]
    public async Task<IActionResult> DisableAccountInvitesAsync([FromBody] DisableInvitesInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        }

        await _accountRepository.UpdateInvitesDisabledAsync(request.Did, true);
        return Ok();
    }
}

public class EnableInvitesInput
{
    public string? Did { get; set; }
}

public class DisableInvitesInput
{
    public string? Did { get; set; }
}
