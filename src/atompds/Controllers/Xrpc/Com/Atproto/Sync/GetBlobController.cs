using AccountManager;
using ActorStore;
using ActorStore.Db;
using BlobStore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[Route("xrpc")]
[ApiController]
public class GetBlobController(
    AccountRepository accountRepository,
    ActorRepositoryProvider actorRepositoryProvider
) : ControllerBase
{

    [HttpGet("com.atproto.sync.getBlob")]
    public async Task<IActionResult> GetBlob(
        [FromQuery] string did,
        [FromQuery] string cid
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

        var cidObject = CID.Cid.FromString(cid);
        

 
        Blob? blob = null;
        Stream? blobStream = null;
        await using (var actorRepo = actorRepositoryProvider.Open(did))
        {
            blob = await actorRepo.Repo.Blob.GetBlob(cidObject);
            if (blob is null)
                throw new XRPCError(new InvalidRequestErrorDetail($"could not find blob for cid: {cid}"));

            if (!string.IsNullOrEmpty(blob.TakedownRef))
                throw new XRPCError(new InvalidRequestErrorDetail($"blob for cid: {cid} is taken down"));

            try
            {
                blobStream = await actorRepo.Repo.Blob.BlobStore.GetStream(cidObject);
            }
            catch (BlobNotFoundException)
            {
                throw new XRPCError(new InvalidRequestErrorDetail($"could not find blob data for cid: {cid}"));
            }
        }

        if (blobStream is null || blob is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find blob data for cid: {cid}"));

        var cancellationToken = Request.HttpContext.RequestAborted;


        HttpContext.Response.ContentLength = blob.Size;
        HttpContext.Response.Headers["x-content-type-options"] = "nosniff";
        HttpContext.Response.Headers["content-security-policy"] = "default-src 'none'; sandbox";

        HttpContext.Response.ContentType = string.IsNullOrEmpty(blob.MimeType) ? "application/octet-stream" : blob.MimeType;

        await blobStream.CopyToAsync(HttpContext.Response.Body, cancellationToken);
        return new EmptyResult();
    }
}
