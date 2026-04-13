using atompds.Middleware;
using Config;
using Crypto;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Identity;

[ApiController]
[Route("xrpc")]
public class GetRecommendedDidCredentialsController : ControllerBase
{
    private readonly ActorStore.ActorRepositoryProvider _actorRepositoryProvider;
    private readonly ILogger<GetRecommendedDidCredentialsController> _logger;
    private readonly SecretsConfig _secretsConfig;

    public GetRecommendedDidCredentialsController(
        ActorStore.ActorRepositoryProvider actorRepositoryProvider,
        SecretsConfig secretsConfig,
        ILogger<GetRecommendedDidCredentialsController> logger)
    {
        _actorRepositoryProvider = actorRepositoryProvider;
        _secretsConfig = secretsConfig;
        _logger = logger;
    }

    [HttpGet("com.atproto.identity.getRecommendedDidCredentials")]
    [AccessPrivileged]
    public async Task<IActionResult> GetRecommendedDidCredentialsAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var signingKey = _actorRepositoryProvider.KeyPair(did);
        var rotationKeyDid = _secretsConfig.PlcRotationKey.Did();

        return Ok(new
        {
            rotationKeys = new[] { rotationKeyDid },
            signingKey = signingKey.Did()
        });
    }
}
