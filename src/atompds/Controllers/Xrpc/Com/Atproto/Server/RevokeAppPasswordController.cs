using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class RevokeAppPasswordController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly AppPasswordStore _appPasswordStore;
    private readonly ILogger<RevokeAppPasswordController> _logger;

    public RevokeAppPasswordController(
        AccountRepository accountRepository,
        AppPasswordStore appPasswordStore,
        ILogger<RevokeAppPasswordController> logger)
    {
        _accountRepository = accountRepository;
        _appPasswordStore = appPasswordStore;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.revokeAppPassword")]
    [AccessPrivileged]
    public async Task<IActionResult> RevokeAppPasswordAsync([FromBody] RevokeAppPasswordInput request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("name is required"));
        }

        var deleted = await _appPasswordStore.DeleteAppPasswordAsync(did, request.Name);
        if (!deleted)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("App password not found"));
        }

        await _accountRepository.RevokeAppPasswordRefreshTokensAsync(did, request.Name);

        return Ok();
    }
}

public class RevokeAppPasswordInput
{
    public string? Name { get; set; }
}
