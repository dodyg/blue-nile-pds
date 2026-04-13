using AccountManager;
using ActorStore;
using CID;
using Repo;
using Xrpc;
using RepoUtil = Repo.Util;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class GetBlocksEndpoints
{
    public static RouteGroupBuilder MapGetBlocksEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.getBlocks", HandleAsync);
        return group;
    }

    private static async Task HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        ILogger<Program> logger,
        string did,
        string[] cids)
    {
        var account = await accountRepository.GetAccountAsync(did, new(true, true));

        if (account is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find account for did: {did}"));

        if (account.TakedownRef is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is taken down"));

        if (account.DeactivatedAt is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is deactivated"));

        var cidObjects = cids.Select(c => Cid.FromString(c)).ToArray();
        logger.LogInformation("Getting {Count} blocks for did {Did}", cidObjects.Length, did);

        BlockMap blocks;
        await using (var actorRepo = actorRepositoryProvider.Open(did))
        {
            var storage = actorRepo.Repo.Storage;
            var (gotBlocks, missing) = await storage.GetBlocksAsync(cidObjects);

            if (missing.Length > 0)
            {
                var missingStr = string.Join(", ", missing.Select(c => c.ToString()));
                throw new XRPCError(new InvalidRequestErrorDetail($"Could not find cids: {missingStr}"));
            }

            blocks = gotBlocks;
        }

        logger.LogInformation("Fetched {Count} blocks for did {Did}", blocks.Size, did);

        var cancellationToken = context.RequestAborted;
        context.Response.ContentType = "application/vnd.ipld.car";

        foreach (var blockBytes in RepoUtil.BlockMapToCarEnumerable(null, blocks))
        {
            await context.Response.Body.WriteAsync(blockBytes, cancellationToken);
        }
    }
}
