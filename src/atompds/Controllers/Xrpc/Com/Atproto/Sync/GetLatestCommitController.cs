using ActorStore;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[ApiController]
[Route("xrpc")]
public class GetLatestCommitController : ControllerBase
{
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly ILogger<GetLatestCommitController> _logger;

    public GetLatestCommitController(ActorRepositoryProvider actorRepositoryProvider, ILogger<GetLatestCommitController> logger)
    {
        _actorRepositoryProvider = actorRepositoryProvider;
        _logger = logger;
    }

    [HttpGet("com.atproto.sync.getLatestCommit")]
    public async Task<IActionResult> GetLatestCommitAsync([FromQuery] string did)
    {
        if (string.IsNullOrWhiteSpace(did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        }

        await using var actorRepo = _actorRepositoryProvider.Open(did);
        var root = await actorRepo.Repo.Storage.GetRootDetailedAsync();

        return Ok(new
        {
            cid = root.Cid.ToString(),
            rev = root.Rev
        });
    }
}
