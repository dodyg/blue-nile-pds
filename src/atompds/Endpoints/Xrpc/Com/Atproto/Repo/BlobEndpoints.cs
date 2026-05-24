using ActorStore;
using ActorStore.Db;
using atompds.Config;
using atompds.Middleware;
using CID;
using Xrpc;
using static ActorStore.Repo.BlobTransactor;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Repo;

public static class BlobEndpoints
{
    public static RouteGroupBuilder MapBlobEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.repo.uploadBlob", HandleAsync).WithMetadata(new AccessStandardAttribute(true, true)).RequireRateLimiting("repo-write");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        ServerConfig serverConfig,
        ActorRepositoryProvider actorRepositoryProvider)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var userSuppliedContentLength = context.Request.ContentLength;
        if (userSuppliedContentLength is null)
            throw new XRPCError(new InvalidRequestErrorDetail("Content-Length header is required"));

        if (userSuppliedContentLength > serverConfig.Service.BlobUploadLimitInBytes)
            throw new XRPCError(new InvalidRequestErrorDetail(
                $"Blob size exceeds maximum upload size of {serverConfig.Service.BlobUploadLimitInBytes} bytes"));

        var userSuppliedContentType = context.Request.ContentType;
        if (string.IsNullOrEmpty(userSuppliedContentType))
            throw new XRPCError(new InvalidRequestErrorDetail("Content-Type header is required"));

        var cancellationToken = context.RequestAborted;
        var fileStream = context.Request.Body;

        await using var actorRepo = actorRepositoryProvider.Open(did);

        var key = await actorRepo.Repo.Blob.BlobStore.PutTempAsync(fileStream, cancellationToken);
        var blobMetaData = await actorRepo.Repo.Blob.GenerateTempBlobMetadataAsync(key, userSuppliedContentType);

        if (blobMetaData.Size != userSuppliedContentLength)
            throw new XRPCError(new InvalidRequestErrorDetail("Uploaded blob size does not match Content-Length header"));

        if (blobMetaData.Size > serverConfig.Service.BlobUploadLimitInBytes)
            throw new XRPCError(new InvalidRequestErrorDetail(
                $"Blob size exceeds maximum upload size of {serverConfig.Service.BlobUploadLimitInBytes} bytes"));

        var (blob, shouldMoveToPermanent) = await actorRepo.TransactRepoAsync<(Blob blob, bool shouldMoveToPermanent)>(async repo =>
        {
            var alreadyExisting = await actorRepo.Repo.Blob.GetBlobAsync(blobMetaData.Cid);
            var blobReferences = await actorRepo.Repo.Blob.GetRecordsForBlobAsync(blobMetaData.Cid);

            if (alreadyExisting is not null)
            {
                if (!string.IsNullOrEmpty(alreadyExisting.TakedownRef))
                    throw new XRPCError(new InvalidRequestErrorDetail("BlobTakedown", "Blob has been taken down and cannot be re-uploaded"));

                if (alreadyExisting.Status == BlobStatus.Permanent)
                    return (alreadyExisting, false);
                else if (alreadyExisting.Status == BlobStatus.Temporary)
                {
                    await actorRepo.Repo.Blob.UpdateBlobAsync(blobMetaData.Cid, b => { b.CreatedAt = DateTime.UtcNow; });
                    return (alreadyExisting, false);
                }
                else
                {
                    await actorRepo.Repo.Blob.UpdateBlobAsync(blobMetaData.Cid, b =>
                    {
                        b.Status = blobReferences.Count > 0 ? BlobStatus.Permanent : BlobStatus.Temporary;
                        b.TempKey = key;
                        b.MimeType = blobMetaData.MimeType;
                        b.Size = (int)blobMetaData.Size;
                        b.CreatedAt = DateTime.UtcNow;
                    });
                    return (alreadyExisting, blobReferences.Count > 0);
                }
            }
            else
            {
                var newBlob = new Blob
                {
                    Cid = blobMetaData.Cid.ToString(),
                    MimeType = blobMetaData.MimeType,
                    Size = (int)blobMetaData.Size,
                    TempKey = key,
                    Status = blobReferences.Count > 0 ? BlobStatus.Permanent : BlobStatus.Temporary,
                    CreatedAt = DateTime.UtcNow
                };
                await actorRepo.Repo.Blob.SaveBlobRecordAsync(newBlob);
                return (newBlob, blobReferences.Count > 0);
            }
        });

        if (shouldMoveToPermanent)
            await actorRepo.Repo.Blob.BlobStore.MakePermanentAsync(key, Cid.FromString(blob.Cid));

        return Results.Ok(new BlobMetaDataResponse(
            MimeType: blobMetaData.MimeType,
            Size: (int)blobMetaData.Size,
            Cid: blobMetaData.Cid.ToString()
        ));
    }

    public record BlobMetaDataResponse(string MimeType, int Size, string Cid);
}
