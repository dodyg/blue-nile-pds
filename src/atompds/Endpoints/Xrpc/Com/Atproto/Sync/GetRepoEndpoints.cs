using AccountManager;
using ActorStore;
using Repo;
using Repo.Car;
using Xrpc;
using RepoUtil = Repo.Util;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class GetRepoEndpoints
{
    public static RouteGroupBuilder MapGetRepoEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.getRepo", HandleAsync);
        return group;
    }

    private static async Task HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        string did,
        string? since = null)
    {
        var account = await accountRepository.GetAccountAsync(did, new(true, true));

        if (account is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find account for did: {did}"));

        if (account.TakedownRef is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is taken down"));

        if (account.DeactivatedAt is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is deactivated"));

        await using var actorRepo = actorRepositoryProvider.Open(did);
        var storage = actorRepo.Repo.Storage;
        var root = await storage.GetRootDetailedAsync();

        var repo = await global::Repo.Repo.LoadAsync(storage, root.Cid);
        var commitBytes = await storage.GetBytesAsync(root.Cid);
        var commitBlock = new CarBlock(root.Cid, commitBytes!);
        var carBlocks = PrependBlockAsync(commitBlock, repo.Data.CarBlockStreamAsync());

        var cancellationToken = context.RequestAborted;
        context.Response.ContentType = "application/vnd.ipld.car";
        await foreach (var blockBytes in RepoUtil.CarBlocksToCarAsyncEnumerableAsync(root.Cid, carBlocks).WithCancellation(cancellationToken))
        {
            await context.Response.Body.WriteAsync(blockBytes, cancellationToken);
        }
    }

    private static async IAsyncEnumerable<CarBlock> PrependBlockAsync(CarBlock first, IAsyncEnumerable<CarBlock> rest)
    {
        yield return first;
        await foreach (var block in rest)
        {
            yield return block;
        }
    }
}
