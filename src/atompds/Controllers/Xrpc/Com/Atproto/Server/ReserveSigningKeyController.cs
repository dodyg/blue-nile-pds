using ActorStore;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class ReserveSigningKeyController : ControllerBase
{
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly ILogger<ReserveSigningKeyController> _logger;

    public ReserveSigningKeyController(ActorRepositoryProvider actorRepositoryProvider, ILogger<ReserveSigningKeyController> logger)
    {
        _actorRepositoryProvider = actorRepositoryProvider;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.reserveSigningKey")]
    [AccessPrivileged]
    public IActionResult ReserveSigningKey()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var keyPair = _actorRepositoryProvider.KeyPair(did);
        var signingKey = keyPair.Did();

        return Ok(new
        {
            signingKey
        });
    }
}
