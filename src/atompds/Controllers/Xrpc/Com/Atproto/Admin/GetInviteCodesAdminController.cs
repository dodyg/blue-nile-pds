using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class GetInviteCodesAdminController : ControllerBase
{
    private readonly InviteStore _inviteStore;
    private readonly ILogger<GetInviteCodesAdminController> _logger;

    public GetInviteCodesAdminController(
        InviteStore inviteStore,
        ILogger<GetInviteCodesAdminController> logger)
    {
        _inviteStore = inviteStore;
        _logger = logger;
    }

    [HttpGet("com.atproto.admin.getInviteCodes")]
    [AdminToken]
    public async Task<IActionResult> GetInviteCodesAsync(
        [FromQuery] string sort = "recent",
        [FromQuery] int limit = 100,
        [FromQuery] string? cursor = null)
    {
        if (limit < 1 || limit > 500)
        {
            limit = 100;
        }

        var (codes, nextCursor) = await _inviteStore.GetInviteCodesAsync(sort, limit, cursor);
        var uses = await _inviteStore.GetInviteCodeUsesAsync(codes.Select(code => code.Code));

        return Ok(new
        {
            cursor = nextCursor,
            codes = codes.Select(code => new
            {
                code = code.Code,
                available = code.AvailableUses,
                disabled = code.Disabled,
                forAccount = code.ForAccount,
                createdBy = code.CreatedBy,
                createdAt = code.CreatedAt.ToString("o"),
                uses = uses.GetValueOrDefault(code.Code, [])
                    .Select(use => new
                    {
                        usedBy = use.UsedBy,
                        usedAt = use.UsedAt.ToString("o")
                    })
            })
        });
    }
}
