using AccountManager;
using ActorStore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[Route("xrpc")]
[ApiController]
public class ListBlobsController(
    ActorRepositoryProvider actorRepositoryProvider,
    AccountRepository accountRepository
) : ControllerBase
{

    [HttpGet("com.atproto.sync.listBlobs")]
    public async Task<IActionResult> ListBlobs(
        [FromQuery] string did,
        [FromQuery] string? since,
        [FromQuery] int limit = 500,
        [FromQuery] string? cursor = null
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


        List<string> blobCids = [];
        await using (var actorRepo = actorRepositoryProvider.Open(did))
        {
            blobCids = await actorRepo.Repo.Blob.ListBlobs(
                since,
                cursor,
                limit
            );
        }


        var last = blobCids.LastOrDefault();

        return Ok(new
        {
            cursor = last ?? string.Empty,
            cids = blobCids
        });
        
    }
        
}
