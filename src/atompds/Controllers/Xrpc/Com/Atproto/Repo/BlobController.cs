using ActorStore;
using ActorStore.Db;
using atompds.Config;
using atompds.Middleware;
using CID;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xrpc;
using static ActorStore.Repo.BlobTransactor;

namespace atompds.Controllers.Xrpc.Com.Atproto.Repo;


[ApiController]
[Route("xrpc")]
public class BlobController(
    ServerConfig serverConfig,
    ActorRepositoryProvider actorRepositoryProvider
) : ControllerBase
{
    
    // TODO: there is some authorization stuff regarding scopes that needs to be done here (consult the reference implemenation)
    [HttpPost("com.atproto.repo.uploadBlob")]
    [AccessStandard(true, true)]
    [EnableRateLimiting("repo-write")]
    public async Task<IActionResult> UploadBlobAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;


        var userSuppliedContentLength = Request.ContentLength;
        if (userSuppliedContentLength is null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Content-Length header is required"));
        }

        if (userSuppliedContentLength > serverConfig.Service.BlobUploadLimitInBytes)
        {
            throw new XRPCError(new InvalidRequestErrorDetail(
                $"Blob size exceeds maximum upload size of {serverConfig.Service.BlobUploadLimitInBytes} bytes"
            ));
        }

        var userSuppliedContentType = Request.ContentType;

        if (string.IsNullOrEmpty(userSuppliedContentType))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Content-Type header is required"));
        }

    
        var cancellationToken = Request.HttpContext.RequestAborted;
        var fileStream = Request.Body;

        await using var actorRepo = actorRepositoryProvider.Open(did);

        var key = await actorRepo.Repo.Blob.BlobStore.PutTempAsync(fileStream, cancellationToken);


        var blobMetaData = await actorRepo.Repo.Blob.GenerateTempBlobMetadataAsync(key, userSuppliedContentType);

        if (blobMetaData.Size != userSuppliedContentLength)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Uploaded blob size does not match Content-Length header"));
        }

        if (blobMetaData.Size > serverConfig.Service.BlobUploadLimitInBytes)
        {
            throw new XRPCError(new InvalidRequestErrorDetail(
                $"Blob size exceeds maximum upload size of {serverConfig.Service.BlobUploadLimitInBytes} bytes"
            ));
        }

        
        var (blob, shouldMoveToPermanent) = await actorRepo.TransactRepoAsync<(Blob blob, bool shouldMoveToPermanent)>(async repo =>
        {
            var alreadyExisting = await actorRepo.Repo.Blob.GetBlobAsync(blobMetaData.Cid);

            var blobReferences = await actorRepo.Repo.Blob.GetRecordsForBlobAsync(blobMetaData.Cid);

            if (alreadyExisting is not null)
            {
                if (!string.IsNullOrEmpty(alreadyExisting.TakedownRef))
                {
                    throw new XRPCError(new InvalidRequestErrorDetail("BlobTakedown", "Blob has been taken down and cannot be re-uploaded"));
                }

                if (alreadyExisting.Status == BlobStatus.Permanent)
                {
                    // we can remove the uploaded file here, or leave for garbage collection to do later
                    return (alreadyExisting, false);
                }
                else if (alreadyExisting.Status == BlobStatus.Temporary)
                {
                    // we can update the tmp key here as the refernce implemntaion
                    // but I don't know how are we going to garbage collect the old temp file then
                    // lets just not update it for now
                    // maybe we can update uploaded at instead?
                    await actorRepo.Repo.Blob.UpdateBlobAsync(blobMetaData.Cid, b =>
                    {
                        b.CreatedAt = DateTime.UtcNow;
                    });
                    return (alreadyExisting, false);
                }
                else // BlobStatus.GarbageCollected
                {
                    await actorRepo.Repo.Blob.UpdateBlobAsync(blobMetaData.Cid, b =>
                    {
                        b.Status = blobReferences.Count > 0 ? BlobStatus.Permanent : BlobStatus.Temporary;
                        b.TempKey = key;
                        b.MimeType = blobMetaData.MimeType;
                        b.Size = (int) blobMetaData.Size;
                        b.CreatedAt = DateTime.UtcNow;
                    });
                    return (alreadyExisting, blobReferences.Count > 0);
                }
            }
            else
            {
                var blob = new Blob
                {
                    Cid = blobMetaData.Cid.ToString(),
                    MimeType = blobMetaData.MimeType,
                    Size = (int) blobMetaData.Size,
                    TempKey = key,
                    Status = blobReferences.Count > 0 ? BlobStatus.Permanent : BlobStatus.Temporary,
                    CreatedAt = DateTime.UtcNow
                };

                await actorRepo.Repo.Blob.SaveBlobRecordAsync(blob);

                return (blob, blobReferences.Count > 0);
            }

        });

        // there is a chance of failure here after the transaction, status in db might be inconsistent with actual blob storage
        if (shouldMoveToPermanent)
        {
            await actorRepo.Repo.Blob.BlobStore.MakePermanentAsync(key, Cid.FromString(blob.Cid));
        }

        return Ok(new BlobMetaDataResponse(
            MimeType: blobMetaData.MimeType,
            Size: (int) blobMetaData.Size,
            Cid: blobMetaData.Cid.ToString()
        ));
    }

    public record BlobMetaDataResponse(
        string MimeType,
        int Size,
        string Cid
    );
}

