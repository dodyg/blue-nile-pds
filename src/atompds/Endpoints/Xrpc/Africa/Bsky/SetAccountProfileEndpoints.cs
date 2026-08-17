using System.Text.Json;
using AccountManager;
using ActorStore;
using ActorStore.Repo;
using AfricaBsky;
using atompds.Config;
using atompds.Middleware;
using CarpaNet;
using CID;
using Config;
using Repo;
using Sequencer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Africa.Bsky;

public static class SetAccountProfileEndpoints
{
    private const string Collection = "africa.bsky.account";
    private const string RecordKey = "self";

    public static RouteGroupBuilder MapSetAccountProfileEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("africa.bsky.setAccountProfile", HandleAsync)
            .WithMetadata(new AccessStandardAttribute())
            .RequireRateLimiting("repo-write");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        SetAccountProfileInput request,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        SequencerRepository sequencer,
        ServerEnvironment env,
        ILogger<Program> logger)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var uri = ATUri.Create(did, Collection, RecordKey);

        await using var actorStore = actorRepositoryProvider.Open(did);
        var current = await actorStore.Record.GetRecordAsync(uri, null, true);

        var record = new Dictionary<string, object?>
        {
            ["$type"] = Collection
        };
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            record["location"] = request.Location;
        }
        if (!string.IsNullOrWhiteSpace(request.AccountType))
        {
            record["accountType"] = request.AccountType;
        }

        record["createdAt"] = current?.Value.TryGetProperty("createdAt", out var existingCreatedAt) == true
            ? existingCreatedAt.GetString() ?? DateTime.UtcNow.ToString("O")
            : DateTime.UtcNow.ToString("O");

        var recordJson = JsonSerializer.SerializeToElement(record);
        var tolerance = TimeSpan.FromMilliseconds(env.PDS_RECORD_CREATED_AT_FUTURE_TOLERANCE_MS);

        IPreparedWrite write;
        if (current != null)
        {
            var preparedUpdate = Prepare.PrepareUpdate(did, Collection, RecordKey, Cid.FromString(current.Cid), recordJson, null, tolerance);
            write = preparedUpdate;
        }
        else
        {
            write = Prepare.PrepareCreate(did, Collection, RecordKey, null, recordJson, null, tolerance);
        }

        await using var db = actorRepositoryProvider.Open(did);
        var commit = await db.Repo.ProcessWritesAsync([write], null);

        await sequencer.SequenceCommitAsync(did, commit, [write]);
        await accountRepository.UpdateRepoRootAsync(did, commit.Cid, commit.Rev);
        await accountRepository.UpdateAccountMetadataAsync(did, request.Location, request.AccountType);

        var (resultUri, resultCid) = write switch
        {
            PreparedCreate create => (create.Uri, create.Cid.ToString()),
            PreparedUpdate update => (update.Uri, update.Cid.ToString()),
            _ => throw new XRPCError(new InvalidRequestErrorDetail("Invalid write."))
        };

        logger.LogInformation("Account profile set for {Did}", did);

        return Results.Ok(new SetAccountProfileOutput
        {
            Uri = resultUri,
            Cid = resultCid
        });
    }
}