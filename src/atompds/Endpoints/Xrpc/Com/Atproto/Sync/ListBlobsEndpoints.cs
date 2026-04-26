using AccountManager;
using ActorStore;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class ListBlobsEndpoints
{
    public static RouteGroupBuilder MapListBlobsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.listBlobs", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        string did,
        string? since,
        int limit = 500,
        string? cursor = null)
    {
        var account = await accountRepository.GetAccountAsync(did, new(true, true));

        if (account is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find account for did: {did}"));

        if (account.TakedownRef is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is taken down"));

        if (account.DeactivatedAt is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is deactivated"));

        List<string> blobCids = [];
        await using (var actorRepo = actorRepositoryProvider.Open(did))
        {
            blobCids = await actorRepo.Repo.Blob.ListBlobsAsync(since, cursor, limit);
        }

        var last = blobCids.LastOrDefault();

        return Results.Ok(new
        {
            cursor = last ?? string.Empty,
            cids = blobCids
        });
    }
}
