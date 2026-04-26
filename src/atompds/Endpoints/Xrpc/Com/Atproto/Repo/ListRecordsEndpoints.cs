using System.Text.Json;
using AccountManager;
using ActorStore;
using CarpaNet;
using ComAtproto.Repo;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Repo;

public static class ListRecordsEndpoints
{
    public static RouteGroupBuilder MapListRecordsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.repo.listRecords", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        string repo,
        string collection,
        ActorRepositoryProvider actorRepositoryProvider,
        AccountRepository accountRepository,
        int limit = 50,
        string? cursor = null,
        bool reverse = false)
    {
        var did = await accountRepository.GetDidForActorAsync(repo);
        if (did is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not find repo: {repo}"));

        await using var actorRepo = actorRepositoryProvider.Open(did);

        var records = await actorRepo.Repo.Record.ListRecordsForCollectionAsync(collection, limit, reverse, cursor);

        var last = records.LastOrDefault();
        ATUri? lastUri = last is not null ? new ATUri(last.Uri) : null;

        return Results.Ok(new ListRecordsOutput
        {
            Cursor = lastUri?.RecordKey,
            Records = records.Select(r => new ListRecordsRecord
            {
                Uri = new ATUri(r.Uri),
                Cid = r.Cid,
                Value = r.Value
            }).ToList()
        });
    }
}
