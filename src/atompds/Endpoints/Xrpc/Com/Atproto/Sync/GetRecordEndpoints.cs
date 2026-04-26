using AccountManager;
using ActorStore;
using CID;
using Repo;
using Repo.Sync;
using Xrpc;
using RepoUtil = Repo.Util;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class GetRecordEndpoints
{
    public static RouteGroupBuilder MapGetRecordEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.getRecord", HandleAsync);
        return group;
    }

    private static async Task HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        string did,
        string collection,
        string rkey)
    {
        var account = await accountRepository.GetAccountAsync(did, new(true, true));

        if (account is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find account for did: {did}"));

        if (account.TakedownRef is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is taken down"));

        if (account.DeactivatedAt is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is deactivated"));

        BlockMap blocks;
        Cid rootCid;
        await using (var actorRepo = actorRepositoryProvider.Open(did))
        {
            var storage = actorRepo.Repo.Storage;
            var commit = await storage.GetRootAsync();
            if (commit is null)
                throw new XRPCError(new InvalidRequestErrorDetail($"could not find commit for did: {did}"));

            (rootCid, blocks) = await Provider.GetRecodsAsync(storage, commit.Value, [(collection, rkey)]);
        }

        var cancellationToken = context.RequestAborted;
        context.Response.ContentType = "application/vnd.ipld.car";

        foreach (var blockBytes in RepoUtil.BlockMapToCarEnumerable(rootCid, blocks))
        {
            await context.Response.Body.WriteAsync(blockBytes, cancellationToken);
        }
    }
}
