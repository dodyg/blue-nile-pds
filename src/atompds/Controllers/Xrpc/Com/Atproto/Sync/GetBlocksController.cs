using AccountManager;
using ActorStore;
using CID;
using Microsoft.AspNetCore.Mvc;
using Repo;
using Xrpc;
using RepoUtil = Repo.Util;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[Route("xrpc")]
[ApiController]
public class GetBlocksController(
    AccountRepository accountRepository,
    ActorRepositoryProvider actorRepositoryProvider,
    ILogger<GetBlocksController> logger
) : ControllerBase
{
    [HttpGet("com.atproto.sync.getBlocks")]
    public async Task<IActionResult> GetBlocksAsync(
        [FromQuery] string did,
        [FromQuery] string[] cids
    )
    {
        // TODO: there is some self and admin stuff that I'm skipping
        var account = await accountRepository.GetAccountAsync(did, new(true, true));

        if (account is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find account for did: {did}"));

        if (account.TakedownRef is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is taken down"));
        
        if (account.DeactivatedAt is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is deactivated"));

        var cidObjects = cids.Select(c => Cid.FromString(c)).ToArray();
        logger.LogInformation("Getting {Count} blocks for did {Did}", cidObjects.Length, did);

        BlockMap blocks;
        await using (var actorRepo = actorRepositoryProvider.Open(did))
        {
            var storage = actorRepo.Repo.Storage;
            var (gotBlocks, missing) = await storage.GetBlocksAsync(cidObjects);
            
            if (missing.Length > 0)
            {
                var missingStr = string.Join(", ", missing.Select(c => c.ToString()));
                throw new XRPCError(new InvalidRequestErrorDetail($"Could not find cids: {missingStr}"));
            }
            
            blocks = gotBlocks;
        }

        logger.LogInformation("Fetched {Count} blocks for did {Did}", blocks.Size, did);

        var cancellationToken = Request.HttpContext.RequestAborted;
        HttpContext.Response.ContentType = "application/vnd.ipld.car";

        foreach (var blockBytes in RepoUtil.BlockMapToCarEnumerable(null, blocks))
        {
            await HttpContext.Response.Body.WriteAsync(blockBytes, cancellationToken);
        }

        return new EmptyResult();
    }
}
