using AccountManager;
using ActorStore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repo;
using Repo.Sync;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[Route("xrpc")]
[ApiController]
public class GetRecordController(
    AccountRepository accountRepository,
    ActorRepositoryProvider actorRepositoryProvider
) : ControllerBase
{
    [HttpGet("com.atproto.sync.getRecord")]
    public async Task<IActionResult> GetRecord(
        [FromQuery] string did,
        [FromQuery] string collection,
        [FromQuery] string rkey
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

        var commit = await storage.GetRoot();

        if (commit is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find commit for did: {did}"));

        // WARNING: this could be very large
        // TODO: Look into how to stream this
        // but these come from the database not sure how to stream that
        var (rootCid, blocks) = await Provider.GetRecods(
            storage,
            commit.Value,
            [
                (collection, rkey)
            ]
        );

        var cancellationToken = Request.HttpContext.RequestAborted;
        HttpContext.Response.ContentType = "application/vnd.ipld.car";

        // maybe we can implement a stream instead of this
        foreach (var blockBytes in Util.BlocksToCarEnumerable(rootCid, blocks))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await HttpContext.Response.Body.WriteAsync(blockBytes, cancellationToken);
        }

        return new EmptyResult();
    }
}
