using AccountManager;
using ActorStore;
using ActorStore.Db;
using BlobStore;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class GetBlobEndpoints
{
    public static RouteGroupBuilder MapGetBlobEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.getBlob", HandleAsync);
        return group;
    }

    private static async Task HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        string did,
        string cid)
    {
        var account = await accountRepository.GetAccountAsync(did, new(true, true));

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
            blob = await actorRepo.Repo.Blob.GetBlobAsync(cidObject);
            if (blob is null)
                throw new XRPCError(new InvalidRequestErrorDetail($"could not find blob for cid: {cid}"));

            if (!string.IsNullOrEmpty(blob.TakedownRef))
                throw new XRPCError(new InvalidRequestErrorDetail($"blob for cid: {cid} is taken down"));

            try
            {
                blobStream = await actorRepo.Repo.Blob.BlobStore.GetStreamAsync(cidObject);
            }
            catch (BlobNotFoundException)
            {
                throw new XRPCError(new InvalidRequestErrorDetail($"could not find blob data for cid: {cid}"));
            }
        }

        if (blobStream is null || blob is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find blob data for cid: {cid}"));

        var cancellationToken = context.RequestAborted;

        context.Response.ContentLength = blob.Size;
        context.Response.Headers["x-content-type-options"] = "nosniff";
        context.Response.Headers["content-security-policy"] = "default-src 'none'; sandbox";
        context.Response.ContentType = string.IsNullOrEmpty(blob.MimeType) ? "application/octet-stream" : blob.MimeType;

        await blobStream.CopyToAsync(context.Response.Body, cancellationToken);
    }
}
