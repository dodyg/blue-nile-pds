using atompds.Services;
using Crypto.Secp256k1;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class ReserveSigningKeyController : ControllerBase
{
    private readonly ReservedSigningKeyStore _reservedSigningKeyStore;
    private readonly ILogger<ReserveSigningKeyController> _logger;

    public ReserveSigningKeyController(ReservedSigningKeyStore reservedSigningKeyStore, ILogger<ReserveSigningKeyController> logger)
    {
        _reservedSigningKeyStore = reservedSigningKeyStore;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.reserveSigningKey")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ReserveSigningKeyAsync([FromBody] ReserveSigningKeyInput? request)
    {
        var signingKey = string.IsNullOrWhiteSpace(request?.Did)
            ? Secp256k1Keypair.Create(false).Did()
            : await _reservedSigningKeyStore.ReserveAsync(request.Did);

        return Ok(new
        {
            signingKey
        });
    }
}

public class ReserveSigningKeyInput
{
    public string? Did { get; set; }
}
