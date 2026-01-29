using AccountManager;
using ActorStore;
using Microsoft.AspNetCore.Mvc;
using Xrpc;
using RepoUtil = Repo.Util;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[Route("xrpc")]
[ApiController]
public class GetRepoController(
    AccountRepository accountRepository,
    ActorRepositoryProvider actorRepositoryProvider
) : ControllerBase
{
    [HttpGet("com.atproto.sync.getRepo")]
    public async Task<IActionResult> GetRepo(
        [FromQuery] string did,
        [FromQuery] string? since = null
    )
    {
        // TODO: there is some self and admin stuff that I'm skipping
        var account = await accountRepository.GetAccount(did, new(true, true));

        if (account is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find account for did: {did}"));

        if (account.TakedownRef is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is taken down"));
        
        if (account.DeactivatedAt is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is deactivated"));


        await using var actorRepo = actorRepositoryProvider.Open(did);
        
        var storage = actorRepo.Repo.Storage;
        var root = await storage.GetRootDetailed();
        
        var carBlocks = storage.IterateCarBlocks(since);
        
        var cancellationToken = Request.HttpContext.RequestAborted;
        HttpContext.Response.ContentType = "application/vnd.ipld.car";
        await foreach (var blockBytes in RepoUtil.CarBlocksToCarAsyncEnumerable(root.Cid, carBlocks).WithCancellation(cancellationToken))
        {
            await HttpContext.Response.Body.WriteAsync(blockBytes, cancellationToken);
        }
        

        return new EmptyResult();
    }
}
