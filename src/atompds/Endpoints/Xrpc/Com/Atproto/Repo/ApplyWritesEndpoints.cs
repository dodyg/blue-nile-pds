using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using ActorStore.Repo;
using atompds.Middleware;
using atompds.Services;
using CarpaNet;
using CarpaNet.Json;
using CID;
using ComAtproto.Repo;
using Config;
using DidLib;
using Handle;
using Identity;
using Repo;
using Sequencer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Repo;

public static class ApplyWritesEndpoints
{
    private static readonly JsonSerializerOptions ApplyWritesJsonOptions = new(ATProtoJsonContext.DefaultOptions)
    {
        AllowOutOfOrderMetadataProperties = true
    };

    public static RouteGroupBuilder MapApplyWritesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.repo.getRecord", GetRecordAsync);
        group.MapPost("com.atproto.repo.putRecord", PutRecordAsync).WithMetadata(new AccessStandardAttribute(true, true)).RequireRateLimiting("repo-write");
        group.MapPost("com.atproto.repo.deleteRecord", DeleteRecordAsync).WithMetadata(new AccessStandardAttribute(true, true)).RequireRateLimiting("repo-write");
        group.MapPost("com.atproto.repo.createRecord", CreateRecordAsync).WithMetadata(new AccessStandardAttribute(true, true)).RequireRateLimiting("repo-write");
        group.MapPost("com.atproto.repo.applyWrites", ApplyWritesAsync).WithMetadata(new AccessStandardAttribute(true, true)).RequireRateLimiting("repo-write");
        return group;
    }

    private static async Task<IResult> GetRecordAsync(
        string repo,
        string collection,
        string rkey,
        string? cid,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        IBskyAppViewConfig bskyAppViewConfig)
    {
        var did = await accountRepository.GetDidForActorAsync(repo);
        if (did == null)
        {
            if (bskyAppViewConfig is BskyAppViewConfig)
                throw new XRPCError(new InvalidRequestErrorDetail("Invalid repo."));
            throw new XRPCError(new InvalidRequestErrorDetail("Could not locate record."));
        }

        await using var db = actorRepositoryProvider.Open(did);
        var uri = ATUri.Create(did, collection, rkey);
        var record = await db.Record.GetRecordAsync(uri, cid);
        if (record == null || record.TakedownRef != null)
            throw new XRPCError(new InvalidRequestErrorDetail("RecordNotFound", $"Could not locate record: {uri}"));

        return Results.Ok(new GetRecordOutput
        {
            Uri = new ATUri(record.Uri),
            Cid = record.Cid,
            Value = record.Value
        });
    }

    private static async Task<IResult> PutRecordAsync(
        HttpContext context,
        JsonDocument json,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        SequencerRepository sequencer,
        WriteSnapshotCache writeSnapshotCache,
        ILogger<Program> logger)
    {
        var tx = PutRecordInput.FromJson(json.RootElement) ?? throw new XRPCError(new InvalidRequestErrorDetail("Invalid record payload."));
        logger.LogInformation("PutRecord: {tx}", tx);

        var did = await CheckAccountAsync(context, tx.Repo, accountRepository);
        var uri = ATUri.Create(did, tx.Collection, tx.Rkey);

        await using var actorStore = actorRepositoryProvider.Open(did);
        var current = await actorStore.Record.GetRecordAsync(uri, null, true);
        var isUpdate = current != null;

        IApplyWritesInputWrites write = isUpdate
            ? new ApplyWritesUpdate { Collection = tx.Collection, Rkey = tx.Rkey, Value = tx.Record }
            : new ApplyWritesCreate { Collection = tx.Collection, Rkey = tx.Rkey, Value = tx.Record };

        var (commit, writeArr) = await HandleAsync(context, tx.Repo, tx.Validate, tx.SwapCommit, tx.SwapRecord, [write], accountRepository, actorRepositoryProvider, sequencer, writeSnapshotCache);
        var commitMeta = new DefsCommitMeta { Cid = commit.Cid.ToString(), Rev = commit.Rev };

        if (isUpdate)
        {
            var writeResult = (PreparedUpdate)writeArr[0];
            return Results.Ok(new PutRecordOutput
            {
                Uri = writeResult.Uri,
                Cid = writeResult.Cid.ToString(),
                Commit = commitMeta,
                ValidationStatus = writeResult.ValidationStatus.ToString()
            });
        }

        var createResult = (PreparedCreate)writeArr[0];
        return Results.Ok(new PutRecordOutput
        {
            Uri = createResult.Uri,
            Cid = createResult.Cid.ToString(),
            Commit = commitMeta,
            ValidationStatus = createResult.ValidationStatus.ToString()
        });
    }

    private static async Task<IResult> DeleteRecordAsync(
        HttpContext context,
        JsonDocument json,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        SequencerRepository sequencer,
        WriteSnapshotCache writeSnapshotCache,
        ILogger<Program> logger)
    {
        var tx = DeleteRecordInput.FromJson(json.RootElement) ?? throw new XRPCError(new InvalidRequestErrorDetail("Invalid delete payload."));
        logger.LogInformation("DeleteRecord: {tx}", tx);
        var (commit, _) = await HandleAsync(context, tx.Repo, false, tx.SwapCommit, tx.SwapRecord, [new ApplyWritesDelete { Collection = tx.Collection, Rkey = tx.Rkey }], accountRepository, actorRepositoryProvider, sequencer, writeSnapshotCache);
        return Results.Ok(new DeleteRecordOutput
        {
            Commit = new DefsCommitMeta { Cid = commit.Cid.ToString(), Rev = commit.Rev }
        });
    }

    private static async Task<IResult> CreateRecordAsync(
        HttpContext context,
        JsonDocument json,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        SequencerRepository sequencer,
        WriteSnapshotCache writeSnapshotCache,
        ILogger<Program> logger)
    {
        var tx = CreateRecordInput.FromJson(json.RootElement) ?? throw new XRPCError(new InvalidRequestErrorDetail("Invalid create payload."));
        logger.LogInformation("CreateRecord: {tx}", tx);
        var (commit, writeArr) = await HandleAsync(context, tx.Repo, tx.Validate, tx.SwapCommit, null, [new ApplyWritesCreate { Collection = tx.Collection, Rkey = tx.Rkey, Value = tx.Record }], accountRepository, actorRepositoryProvider, sequencer, writeSnapshotCache);
        var write = (PreparedCreate)writeArr[0];
        return Results.Ok(new CreateRecordOutput
        {
            Uri = write.Uri,
            Cid = commit.Cid.ToString(),
            Commit = new DefsCommitMeta { Cid = commit.Cid.ToString(), Rev = commit.Rev },
            ValidationStatus = write.ValidationStatus.ToString()
        });
    }

    private static async Task<IResult> ApplyWritesAsync(
        HttpContext context,
        JsonDocument json,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        SequencerRepository sequencer,
        WriteSnapshotCache writeSnapshotCache,
        ILogger<Program> logger)
    {
        var tx = JsonSerializer.Deserialize<ApplyWritesInput>(json.RootElement.GetRawText(), ApplyWritesJsonOptions)
                 ?? throw new XRPCError(new InvalidRequestErrorDetail("Invalid applyWrites payload."));
        logger.LogInformation("ApplyWrites: {tx}", tx);
        var (commit, writeArr) = await HandleAsync(context, tx.Repo, tx.Validate, tx.SwapCommit, null, tx.Writes, accountRepository, actorRepositoryProvider, sequencer, writeSnapshotCache);
        return Results.Ok(new ApplyWritesOutput
        {
            Commit = new DefsCommitMeta { Cid = commit.Cid.ToString(), Rev = commit.Rev },
            Results = writeArr.Select(WriteToOutputResult).ToList()
        });
    }

    private static async Task<string> CheckAccountAsync(HttpContext context, ATIdentifier repo, AccountRepository accountRepository)
    {
        var handleOrDid = repo.Value;
        var auth = context.GetAuthOutput();
        var account = await accountRepository.GetAccountAsync(handleOrDid, new AvailabilityFlags(IncludeDeactivated: true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not find repo: {repo}"));
        if (account.DeactivatedAt != null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account is deactivated."));

        var did = account.Did;
        if (did != auth.AccessCredentials.Did)
            throw new XRPCError(new AuthRequiredErrorDetail("Invalid did."));

        return did;
    }

    private static async Task<(CommitData commit, IPreparedWrite[] writeArr)> HandleAsync(
        HttpContext context,
        ATIdentifier repo,
        bool? validate,
        string? swapCommit,
        string? swapRecord,
        IReadOnlyList<IApplyWritesInputWrites>? writeOps,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        SequencerRepository sequencer,
        WriteSnapshotCache writeSnapshotCache)
    {
        var did = await CheckAccountAsync(context, repo, accountRepository);
        if (writeOps == null || writeOps.Count > 200)
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid writes."));

        var writes = new List<IPreparedWrite>();
        foreach (var write in writeOps)
        {
            switch (write)
            {
                case ApplyWritesCreate create:
                {
                    if (string.IsNullOrWhiteSpace(create.Collection) || create.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid create."));
                    var preparedCreate = Prepare.PrepareCreate(did, create.Collection, create.Rkey, null, create.Value, validate);
                    writes.Add(preparedCreate);
                    break;
                }
                case ApplyWritesUpdate update:
                {
                    if (string.IsNullOrWhiteSpace(update.Collection) || string.IsNullOrWhiteSpace(update.Rkey) ||
                        update.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid update."));
                    Cid? swapRecordCid = swapRecord != null ? Cid.FromString(swapRecord) : null;
                    var preparedUpdate = Prepare.PrepareUpdate(did, update.Collection, update.Rkey, swapRecordCid, update.Value, validate);
                    writes.Add(preparedUpdate);
                    break;
                }
                case ApplyWritesDelete delete:
                {
                    if (string.IsNullOrWhiteSpace(delete.Collection) || string.IsNullOrWhiteSpace(delete.Rkey))
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid delete."));
                    var preparedDelete = Prepare.PrepareDelete(did, delete.Collection, delete.Rkey, swapRecord != null ? Cid.FromString(swapRecord) : null);
                    writes.Add(preparedDelete);
                    break;
                }
                default:
                    throw new XRPCError(new InvalidRequestErrorDetail("Action not supported."));
            }
        }

        Cid? swapCommitCid = swapCommit != null ? Cid.FromString(swapCommit) : null;
        var writeArr = writes.ToArray();
        await using var db = actorRepositoryProvider.Open(did);
        var commit = await db.Repo.ProcessWritesAsync(writeArr, swapCommitCid);

        await sequencer.SequenceCommitAsync(did, commit, writeArr);
        await accountRepository.UpdateRepoRootAsync(did, commit.Cid, commit.Rev);

        foreach (var w in writeArr)
        {
            switch (w)
            {
                case PreparedCreate c:
                    writeSnapshotCache.AddWrite(did, c.Uri.Collection ?? "", c.Uri.RecordKey ?? "", c.Record?.ToJSONString() ?? "{}", c.Cid.ToString(), commit.Rev);
                    break;
                case PreparedUpdate u:
                    writeSnapshotCache.AddWrite(did, u.Uri.Collection ?? "", u.Uri.RecordKey ?? "", u.Record?.ToJSONString() ?? "{}", u.Cid.ToString(), commit.Rev);
                    break;
                case PreparedDelete d:
                    writeSnapshotCache.RemoveWrite(did, d.Uri.Collection ?? "", d.Uri.RecordKey ?? "");
                    break;
            }
        }

        return (commit, writeArr);
    }

    private static IApplyWritesOutputResults WriteToOutputResult(IPreparedWrite write)
    {
        return write switch
        {
            PreparedCreate create => new ApplyWritesCreateResult
            {
                Uri = create.Uri,
                Cid = create.Cid.ToString(),
                ValidationStatus = create.ValidationStatus.ToString()
            },
            PreparedUpdate update => new ApplyWritesUpdateResult
            {
                Uri = update.Uri,
                Cid = update.Cid.ToString(),
                ValidationStatus = update.ValidationStatus.ToString()
            },
            PreparedDelete => new ApplyWritesDeleteResult(),
            _ => throw new XRPCError(new InvalidRequestErrorDetail("Invalid write type."))
        };
    }
}
