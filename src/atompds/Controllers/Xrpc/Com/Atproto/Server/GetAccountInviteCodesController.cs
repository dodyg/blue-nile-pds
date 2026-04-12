using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class GetAccountInviteCodesController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly InviteStore _inviteStore;
    private readonly ILogger<GetAccountInviteCodesController> _logger;

    public GetAccountInviteCodesController(
        AccountRepository accountRepository,
        InviteStore inviteStore,
        ILogger<GetAccountInviteCodesController> logger)
    {
        _accountRepository = accountRepository;
        _inviteStore = inviteStore;
        _logger = logger;
    }

    [HttpGet("com.atproto.server.getAccountInviteCodes")]
    [AccessStandard]
    public async Task<IActionResult> GetAccountInviteCodesAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var codes = await _inviteStore.GetInviteCodesForAccountAsync(did);

        return Ok(new
        {
            codes = codes.Select(c => new
            {
                code = c.Code,
                available = c.AvailableUses,
                disabled = c.Disabled,
                forAccount = c.ForAccount,
                createdBy = c.CreatedBy,
                createdAt = c.CreatedAt.ToString("o")
            })
        });
    }
}
