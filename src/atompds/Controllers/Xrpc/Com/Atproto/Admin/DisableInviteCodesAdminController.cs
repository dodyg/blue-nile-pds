using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class DisableInviteCodesAdminController : ControllerBase
{
    private readonly InviteStore _inviteStore;
    private readonly ILogger<DisableInviteCodesAdminController> _logger;

    public DisableInviteCodesAdminController(
        InviteStore inviteStore,
        ILogger<DisableInviteCodesAdminController> logger)
    {
        _inviteStore = inviteStore;
        _logger = logger;
    }

    [HttpPost("com.atproto.admin.disableInviteCodes")]
    [AdminToken]
    public async Task<IActionResult> DisableInviteCodesAsync([FromBody] DisableInviteCodesInput? request)
    {
        await _inviteStore.DisableInviteCodesAsync(
            request?.Codes ?? [],
            request?.Accounts ?? []);

        return Ok();
    }
}

public class DisableInviteCodesInput
{
    public List<string> Codes { get; set; } = [];
    public List<string> Accounts { get; set; } = [];
}
