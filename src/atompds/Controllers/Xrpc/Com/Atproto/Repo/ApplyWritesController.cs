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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Repo;
using Sequencer;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Repo;

[ApiController]
[Route("xrpc")]
public class ApplyWritesController : ControllerBase
{
    private static readonly JsonSerializerOptions ApplyWritesJsonOptions = new(ATProtoJsonContext.DefaultOptions)
    {
        AllowOutOfOrderMetadataProperties = true
    };

    private readonly AccountRepository _accountRepository;
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly IBskyAppViewConfig _bskyAppViewConfig;
    private readonly ILogger<ApplyWritesController> _logger;
    private readonly SequencerRepository _sequencer;
    private readonly WriteSnapshotCache _writeSnapshotCache;

    public ApplyWritesController(
        ILogger<ApplyWritesController> logger,
        AccountRepository accountRepository,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        InvitesConfig invitesConfig,
        HttpClient httpClient,
        HandleManager handle,
        ActorRepositoryProvider actorRepositoryProvider,
        IdResolver idResolver,
        SecretsConfig secretsConfig,
        SequencerRepository sequencer,
        IBskyAppViewConfig bskyAppViewConfig,
        PlcClient plcClient,
        WriteSnapshotCache writeSnapshotCache)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _actorRepositoryProvider = actorRepositoryProvider;
        _sequencer = sequencer;
        _bskyAppViewConfig = bskyAppViewConfig;
        _writeSnapshotCache = writeSnapshotCache;
    }

    [HttpGet("com.atproto.repo.getRecord")]
    public async Task<IActionResult> GetRecordAsync([FromQuery] string repo, [FromQuery] string collection, [FromQuery] string rkey, [FromQuery] string? cid)
    {
        var did = await _accountRepository.GetDidForActorAsync(repo);
        if (did == null)
        {
            if (_bskyAppViewConfig is BskyAppViewConfig)
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Invalid repo."));
            }

            throw new XRPCError(new InvalidRequestErrorDetail("Could not locate record."));
        }

        await using var db = _actorRepositoryProvider.Open(did);
        var uri = ATUri.Create(did, collection, rkey);
        var record = await db.Record.GetRecordAsync(uri, cid);
        if (record == null || record.TakedownRef != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("RecordNotFound", $"Could not locate record: {uri}"));
        }

        return Ok(new GetRecordOutput
        {
            Uri = new ATUri(record.Uri),
            Cid = record.Cid,
            Value = record.Value
        });
    }

    [HttpPost("com.atproto.repo.putRecord")]
    [AccessStandard(true, true)]
    [EnableRateLimiting("repo-write")]
    public async Task<IActionResult> PutRecordAsync(JsonDocument json)
    {
        var tx = PutRecordInput.FromJson(json.RootElement) ?? throw new XRPCError(new InvalidRequestErrorDetail("Invalid record payload."));
        _logger.LogInformation("PutRecord: {tx}", tx);

        var did = await CheckAccountAsync(tx.Repo);
        var uri = ATUri.Create(did, tx.Collection, tx.Rkey);

        await using var actorStore = _actorRepositoryProvider.Open(did);
        var current = await actorStore.Record.GetRecordAsync(uri, null, true);
        var isUpdate = current != null;

        IApplyWritesInputWrites write = isUpdate
            ? new ApplyWritesUpdate { Collection = tx.Collection, Rkey = tx.Rkey, Value = tx.Record }
            : new ApplyWritesCreate { Collection = tx.Collection, Rkey = tx.Rkey, Value = tx.Record };

        var (commit, writeArr) = await HandleAsync(tx.Repo, tx.Validate, tx.SwapCommit, tx.SwapRecord, [write]);
        var commitMeta = new DefsCommitMeta { Cid = commit.Cid.ToString(), Rev = commit.Rev };

        if (isUpdate)
        {
            var writeResult = (PreparedUpdate)writeArr[0];
            return Ok(new PutRecordOutput
            {
                Uri = writeResult.Uri,
                Cid = writeResult.Cid.ToString(),
                Commit = commitMeta,
                ValidationStatus = writeResult.ValidationStatus.ToString()
            });
        }

        var createResult = (PreparedCreate)writeArr[0];
        return Ok(new PutRecordOutput
        {
            Uri = createResult.Uri,
            Cid = createResult.Cid.ToString(),
            Commit = commitMeta,
            ValidationStatus = createResult.ValidationStatus.ToString()
        });
    }

    [HttpPost("com.atproto.repo.deleteRecord")]
    [AccessStandard(true, true)]
    [EnableRateLimiting("repo-write")]
    public async Task<IActionResult> DeleteRecordAsync(JsonDocument json)
    {
        var tx = DeleteRecordInput.FromJson(json.RootElement) ?? throw new XRPCError(new InvalidRequestErrorDetail("Invalid delete payload."));

        _logger.LogInformation("DeleteRecord: {tx}", tx);
        var (commit, _) = await HandleAsync(tx.Repo, false, tx.SwapCommit, tx.SwapRecord, [new ApplyWritesDelete { Collection = tx.Collection, Rkey = tx.Rkey }]);
        return Ok(new DeleteRecordOutput
        {
            Commit = new DefsCommitMeta { Cid = commit.Cid.ToString(), Rev = commit.Rev }
        });
    }

    [HttpPost("com.atproto.repo.createRecord")]
    [AccessStandard(true, true)]
    [EnableRateLimiting("repo-write")]
    public async Task<IActionResult> createRecordAsync(JsonDocument json)
    {
        var tx = CreateRecordInput.FromJson(json.RootElement) ?? throw new XRPCError(new InvalidRequestErrorDetail("Invalid create payload."));
        _logger.LogInformation("CreateRecord: {tx}", tx);
        var (commit, writeArr) = await HandleAsync(tx.Repo, tx.Validate, tx.SwapCommit, null, [new ApplyWritesCreate { Collection = tx.Collection, Rkey = tx.Rkey, Value = tx.Record }]);
        var write = (PreparedCreate)writeArr[0];
        return Ok(new CreateRecordOutput
        {
            Uri = write.Uri,
            Cid = commit.Cid.ToString(),
            Commit = new DefsCommitMeta { Cid = commit.Cid.ToString(), Rev = commit.Rev },
            ValidationStatus = write.ValidationStatus.ToString()
        });
    }

    [HttpPost("com.atproto.repo.applyWrites")]
    [AccessStandard(true, true)]
    [EnableRateLimiting("repo-write")]
    public async Task<IActionResult> ApplyWritesAsync(JsonDocument json)
    {
        var tx = JsonSerializer.Deserialize<ApplyWritesInput>(json.RootElement.GetRawText(), ApplyWritesJsonOptions)
                 ?? throw new XRPCError(new InvalidRequestErrorDetail("Invalid applyWrites payload."));
        _logger.LogInformation("ApplyWrites: {tx}", tx);
        var (commit, writeArr) = await HandleAsync(tx.Repo, tx.Validate, tx.SwapCommit, null, tx.Writes);
        return Ok(new ApplyWritesOutput
        {
            Commit = new DefsCommitMeta { Cid = commit.Cid.ToString(), Rev = commit.Rev },
            Results = writeArr.Select(WriteToOutputResult).ToList()
        });
    }

    private async Task<string> CheckAccountAsync(ATIdentifier repo)
    {
        var handleOrDid = repo.Value;
        var auth = HttpContext.GetAuthOutput();
        var account = await _accountRepository.GetAccountAsync(handleOrDid, new AvailabilityFlags(IncludeDeactivated: true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not find repo: {repo}"));
        }
        if (account.DeactivatedAt != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account is deactivated."));
        }

        var did = account.Did;
        if (did != auth.AccessCredentials.Did)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Invalid did."));
        }

        return did;
    }

    private async Task<(CommitData commit, IPreparedWrite[] writeArr)> HandleAsync(
        ATIdentifier repo,
        bool? validate,
        string? swapCommit,
        string? swapRecord,
        IReadOnlyList<IApplyWritesInputWrites>? writeOps)
    {
        var did = await CheckAccountAsync(repo);
        if (writeOps == null || writeOps.Count > 200)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid writes."));
        }

        var writes = new List<IPreparedWrite>();
        foreach (var write in writeOps)
        {
            switch (write)
            {
                case ApplyWritesCreate create:
                {
                    if (string.IsNullOrWhiteSpace(create.Collection) || create.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid create."));
                    }
                    var preparedCreate = Prepare.PrepareCreate(did, create.Collection, create.Rkey, null, create.Value, validate);
                    writes.Add(preparedCreate);
                    break;
                }
                case ApplyWritesUpdate update:
                {
                    if (string.IsNullOrWhiteSpace(update.Collection) || string.IsNullOrWhiteSpace(update.Rkey) ||
                        update.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid update."));
                    }
                    Cid? swapRecordCid = swapRecord != null ? Cid.FromString(swapRecord) : null;
                    var preparedUpdate = Prepare.PrepareUpdate(did, update.Collection, update.Rkey, swapRecordCid, update.Value, validate);
                    writes.Add(preparedUpdate);
                    break;
                }
                case ApplyWritesDelete delete:
                {
                    if (string.IsNullOrWhiteSpace(delete.Collection) || string.IsNullOrWhiteSpace(delete.Rkey))
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid delete."));
                    }
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
        await using var db = _actorRepositoryProvider.Open(did);
        var commit = await db.Repo.ProcessWritesAsync(writeArr, swapCommitCid);

        await _sequencer.SequenceCommitAsync(did, commit, writeArr);
        await _accountRepository.UpdateRepoRootAsync(did, commit.Cid, commit.Rev);

        foreach (var w in writeArr)
        {
            switch (w)
            {
                case PreparedCreate c:
                    _writeSnapshotCache.AddWrite(did, c.Uri.Collection ?? "", c.Uri.RecordKey ?? "",
                        c.Record?.ToJSONString() ?? "{}", c.Cid.ToString(), commit.Rev);
                    break;
                case PreparedUpdate u:
                    _writeSnapshotCache.AddWrite(did, u.Uri.Collection ?? "", u.Uri.RecordKey ?? "",
                        u.Record?.ToJSONString() ?? "{}", u.Cid.ToString(), commit.Rev);
                    break;
                case PreparedDelete d:
                    _writeSnapshotCache.RemoveWrite(did, d.Uri.Collection ?? "", d.Uri.RecordKey ?? "");
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
            _ => throw new Exception("Invalid write type.")
        };
    }
}
