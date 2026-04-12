using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class CreateInviteCodesController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly InviteStore _inviteStore;
    private readonly ILogger<CreateInviteCodesController> _logger;

    public CreateInviteCodesController(
        AccountRepository accountRepository,
        InviteStore inviteStore,
        ILogger<CreateInviteCodesController> logger)
    {
        _accountRepository = accountRepository;
        _inviteStore = inviteStore;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.createInviteCodes")]
    [AccessPrivileged]
    public async Task<IActionResult> CreateInviteCodesAsync([FromBody] CreateInviteCodesInput request)
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
        var codes = new List<string>();
        for (var i = 0; i < request.CodeCount; i++)
        {
            var code = await _inviteStore.CreateInviteCodeAsync(did, did, useCount);
            codes.Add(code);
        }

        return Ok(new { codes });
    }
}

public class CreateInviteCodesInput
{
    public int CodeCount { get; set; } = 1;
    public int UseCount { get; set; } = 1;
}
