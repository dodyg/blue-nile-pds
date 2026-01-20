using ActorStore;
using ActorStore.Db;
using atompds.Config;
using atompds.Middleware;
using CID;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    // TODO: rate limiting
    [HttpPost("com.atproto.repo.uploadBlob")]
    [AccessStandard(true, true)]
    public async Task<IActionResult> UploadBlob()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;


        var userSuppliedContentLength = Request.ContentLength;
        if (userSuppliedContentLength is null)
        {
            return BadRequest("Content-Length header is required");
        }

        if (userSuppliedContentLength > serverConfig.Service.BlobUploadLimitInBytes)
        {
            return BadRequest($"Blob size exceeds maximum upload size of {serverConfig.Service.BlobUploadLimitInBytes} bytes");
        }

        var userSuppliedContentType = Request.ContentType;

        if (string.IsNullOrEmpty(userSuppliedContentType))
        {
            return BadRequest("Content-Type header is required");
        }

    
        var cancellationToken = Request.HttpContext.RequestAborted;
        var fileStream = Request.Body;

        using var actorRepo = actorRepositoryProvider.Open(did);

        var key = await actorRepo.Repo.Blob.BlobStore.PutTemp(fileStream, cancellationToken);


        var blobMetaData = await actorRepo.Repo.Blob.GenerateTempBlobMetadata(key, userSuppliedContentType);

        if (blobMetaData.Size != userSuppliedContentLength)
        {
            return BadRequest("Uploaded blob size does not match Content-Length header");
        }

        if (blobMetaData.Size > serverConfig.Service.BlobUploadLimitInBytes)
        {
            return BadRequest($"Blob size exceeds maximum upload size of {serverConfig.Service.BlobUploadLimitInBytes} bytes");
        }

        
        var _ = await actorRepo.TransactRepo<Blob>(async repo =>
        {
            var alreadyExisting = await actorRepo.Repo.Blob.GetBlob(blobMetaData.Cid);

            if (alreadyExisting is not null)
            {
                if (!string.IsNullOrEmpty(alreadyExisting.TakedownRef))
                {
                    throw new Exception("Blob has been taken down and cannot be re-uploaded");
                }

                if (alreadyExisting.Status == BlobStatus.Permanent)
                {
                    // we can remove the uploaded file here, or leave for garbage collection to do later
                    return alreadyExisting;
                }

                if (alreadyExisting.Status == BlobStatus.Temporary)
                {
                    // we can update the tmp key here as the refernce implemntaion
                    // but I don't know how are we going to garbage collect the old temp file then
                    // lets just not update it for now
                    // maybe we can update uploaded at instead?
                    await actorRepo.Repo.Blob.UpdateBlob(blobMetaData.Cid, b =>
                    {
                        b.CreatedAt = DateTime.UtcNow;
                    });
                    return alreadyExisting;
                }

                if (alreadyExisting.Status == BlobStatus.GarbageCollected)
                {
                    await actorRepo.Repo.Blob.UpdateBlob(blobMetaData.Cid, b =>
                    {
                        b.Status = BlobStatus.Temporary;
                        b.TempKey = key;
                        b.MimeType = blobMetaData.MimeType;
                        b.Size = (int) blobMetaData.Size;
                        b.CreatedAt = DateTime.UtcNow;
                    });
                }


                // TODO: need to check if the blob is referenced by any records
                // from what I understand this can happen in the process of migrating users as the specs say
                // in this case we need to move the blob to permanent storage


            }

            var blob = new Blob
            {
                Cid = blobMetaData.Cid.ToString(),
                MimeType = blobMetaData.MimeType,
                Size = (int) blobMetaData.Size,
                TempKey = key,
                Status = BlobStatus.Temporary,
                CreatedAt = DateTime.UtcNow
            };

            await actorRepo.Repo.Blob.SaveBlobRecord(blob);

            return blob;
        });

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

