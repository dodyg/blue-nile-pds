using ActorStore;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class GetLatestCommitEndpoints
{
    public static RouteGroupBuilder MapGetLatestCommitEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.getLatestCommit", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        ActorRepositoryProvider actorRepositoryProvider,
        string did)
    {
        if (string.IsNullOrWhiteSpace(did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));

        await using var actorRepo = actorRepositoryProvider.Open(did);
        var root = await actorRepo.Repo.Storage.GetRootDetailedAsync();

        return Results.Ok(new
        {
            cid = root.Cid.ToString(),
            rev = root.Rev
        });
    }
}
