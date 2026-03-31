using AccountManager;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class DeleteSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<DeleteSessionController> _logger;

    public DeleteSessionController(AccountRepository accountRepository,
        ILogger<DeleteSessionController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.deleteSession")]
    [Refresh]
    public async Task<IActionResult> DeleteSession()
    {
        var auth = HttpContext.GetRefreshOutput();
        var tokenId = auth.RefreshCredentials.TokenId;
        await _accountRepository.RevokeRefreshToken(tokenId);
        return Ok();
    }
}
