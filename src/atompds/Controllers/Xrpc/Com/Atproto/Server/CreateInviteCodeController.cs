using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class CreateInviteCodeController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly InviteStore _inviteStore;
    private readonly ILogger<CreateInviteCodeController> _logger;

    public CreateInviteCodeController(
        AccountRepository accountRepository,
        InviteStore inviteStore,
        ILogger<CreateInviteCodeController> logger)
    {
        _accountRepository = accountRepository;
        _inviteStore = inviteStore;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.createInviteCode")]
    [AccessPrivileged]
    public async Task<IActionResult> CreateInviteCodeAsync([FromBody] CreateInviteCodeInput request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await _accountRepository.GetAccountAsync(did);
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        if (account.InvitesDisabled == true)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Invites are disabled for this account"));
        }

        var useCount = request.UseCount > 0 ? request.UseCount : 1;
        var code = await _inviteStore.CreateInviteCodeAsync(did, did, useCount);

        return Ok(new { code });
    }
}

public class CreateInviteCodeInput
{
    public int UseCount { get; set; } = 1;
    public string? ForAccount { get; set; }
}
