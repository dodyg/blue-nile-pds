using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using ActorStore.Repo;
using atompds.Middleware;
using CID;
using Config;
using DidLib;
using FishyFlip.Lexicon;
using FishyFlip.Lexicon.Com.Atproto.Repo;
using FishyFlip.Models;
using Handle;
using Identity;
using Microsoft.AspNetCore.Mvc;
using Repo;
using Sequencer;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Repo;

[ApiController]
[Route("xrpc")]
public class ApplyWritesController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly IBskyAppViewConfig _bskyAppViewConfig;
    private readonly HandleManager _handle;
    private readonly HttpClient _httpClient;
    private readonly IdentityConfig _identityConfig;
    private readonly IdResolver _idResolver;
    private readonly InvitesConfig _invitesConfig;
    private readonly ILogger<ApplyWritesController> _logger;
    private readonly PlcClient _plcClient;
    private readonly SecretsConfig _secretsConfig;
    private readonly SequencerRepository _sequencer;
    private readonly ServiceConfig _serviceConfig;

    public ApplyWritesController(ILogger<ApplyWritesController> logger,
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
        PlcClient plcClient)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _invitesConfig = invitesConfig;
        _httpClient = httpClient;
        _handle = handle;
        _actorRepositoryProvider = actorRepositoryProvider;
        _idResolver = idResolver;
        _secretsConfig = secretsConfig;
        _sequencer = sequencer;
        _bskyAppViewConfig = bskyAppViewConfig;
        _plcClient = plcClient;
    }

    [HttpGet("com.atproto.repo.getRecord")]
    public async Task<IActionResult> GetRecord([FromQuery] string repo, [FromQuery] string collection, [FromQuery] string rkey, [FromQuery] string? cid)
    {
        var did = await _accountRepository.GetDidForActor(repo);
        if (did == null)
        {
            if (_bskyAppViewConfig is BskyAppViewConfig bskyAppViewConfig)
            {
                // TODO: pipe to appview
                throw new XRPCError(new InvalidRequestErrorDetail("Invalid repo."));
            }

            throw new XRPCError(new InvalidRequestErrorDetail("Could not locate record."));
        }

        await using var db = _actorRepositoryProvider.Open(did);
        var uri = ATUri.Create($"{did}/{collection}/{rkey}");
        var record = await db.Record.GetRecord(uri, cid);
        if (record == null || record.TakedownRef != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("RecordNotFound", $"Could not locate record: {uri}"));
        }

        return Ok(new GetRecordOutput(ATUri.Create(record.Uri), record.Cid, record.Value.ToATObject()));
    }

    [HttpPost("com.atproto.repo.putRecord")]
    [AccessStandard(true, true)]
    public async Task<IActionResult> PutRecord(JsonDocument json)
    {
        var tx = JsonSerializer.Deserialize<PutRecordInput>(json.RootElement.GetRawText(), new JsonSerializerOptions
        {
            AllowOutOfOrderMetadataProperties = true
        });
        _logger.LogInformation("PutRecord: {tx}", tx);

        var did = await CheckAccount(tx.Repo);
        var uri = ATUri.Create($"{did}/{tx.Collection}/{tx.Rkey}");

        await using var actorStore = _actorRepositoryProvider.Open(did);
        var current = await actorStore.Record.GetRecord(uri, null, true);
        var isUpdate = current != null;

        ATObject write = isUpdate
            ? new Update(tx.Collection, tx.Rkey, tx.Record)
            : new Create(tx.Collection, tx.Rkey, tx.Record);

        var (commit, writeArr) = await Handle(tx.Repo, tx.Validate, tx.SwapCommit, tx.SwapRecord, [write]);

        if (isUpdate)
        {
            var writeResult = (PreparedUpdate)writeArr[0];
            return Ok(new PutRecordOutput(writeResult.Uri, writeResult.Cid.ToString(), new CommitMeta(commit.Cid.ToString(), commit.Rev),
                writeResult.ValidationStatus.ToString()));
        }
        else
        {
            var writeResult = (PreparedCreate)writeArr[0];
            return Ok(new PutRecordOutput(writeResult.Uri, writeResult.Cid.ToString(), new CommitMeta(commit.Cid.ToString(), commit.Rev),
                writeResult.ValidationStatus.ToString()));
        }
    }


    [HttpPost("com.atproto.repo.deleteRecord")]
    [AccessStandard(true, true)]
    public async Task<IActionResult> DeleteRecord(JsonDocument json)
    {
        var tx = JsonSerializer.Deserialize<DeleteRecordInput>(json.RootElement.GetRawText(), new JsonSerializerOptions
        {
            AllowOutOfOrderMetadataProperties = true
        });

        _logger.LogInformation("DeleteRecord: {tx}", tx);
        var (commit, writeArr) = await Handle(tx.Repo, false, tx.SwapCommit, tx.SwapRecord, [new Delete(tx.Collection, tx.Rkey)]);
        return Ok(new DeleteRecordOutput(new CommitMeta(commit.Cid.ToString(), commit.Rev)));
    }


    [HttpPost("com.atproto.repo.createRecord")]
    [AccessStandard(true, true)]
    public async Task<IActionResult> ApplyWrites(JsonDocument json)
    {
        var tx = JsonSerializer.Deserialize<CreateRecordInput>(json.RootElement.GetRawText(), new JsonSerializerOptions
        {
            AllowOutOfOrderMetadataProperties = true
        });
        _logger.LogInformation("CreateRecord: {tx}", tx);
        var (commit, writeArr) = await Handle(tx.Repo, tx.Validate, tx.SwapCommit, null, [new Create(tx.Collection, tx.Rkey, tx.Record)]);
        var write = (PreparedCreate)writeArr[0];
        return Ok(new CreateRecordOutput(write.Uri, commit.Cid.ToString(), new CommitMeta(commit.Cid.ToString(), commit.Rev), write.ValidationStatus.ToString()));
    }

    [HttpPost("com.atproto.repo.applyWrites")]
    [AccessStandard(true, true)]
    public async Task<IActionResult> ApplyWrites([FromBody] ApplyWritesInput tx)
    {
        _logger.LogInformation("ApplyWrites: {tx}", tx);
        var (commit, writeArr) = await Handle(tx.Repo, tx.Validate, tx.SwapCommit, null, tx.Writes);
        return Ok(new ApplyWritesOutput(new CommitMeta(commit.Cid.ToString(), commit.Rev), writeArr.Select(WriteToOutputResult).ToList()));
    }

    private async Task<string> CheckAccount(ATIdentifier? repo)
    {
        string handleOrDid;
        if (repo is ATHandle atHandle)
        {
            handleOrDid = atHandle.Handle;
        }
        else if (repo is ATDid atDid)
        {
            handleOrDid = atDid.Handler;
        }
        else
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid repo type."));
        }

        var auth = HttpContext.GetAuthOutput();
        var account = await _accountRepository.GetAccount(handleOrDid, new AvailabilityFlags(IncludeDeactivated: true));
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

    private async Task<(CommitData commit, IPreparedWrite[] writeArr)> Handle(ATIdentifier? repo,
        bool? validate,
        string? swapCommit,
        string? swapRecord,
        List<ATObject>? writeOps)
    {
        var did = await CheckAccount(repo);
        if (writeOps == null || writeOps.Count > 200)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid writes."));
        }

        var writes = new List<IPreparedWrite>();
        foreach (var write in writeOps)
        {
            switch (write.Type)
            {
                case "com.atproto.repo.applyWrites#create":
                {
                    var create = (Create)write;
                    if (create.Collection == null || create.Value == null)
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid create."));
                    }
                    var preparedCreate = Prepare.PrepareCreate(did, create.Collection, create.Rkey, null, create.Value, validate);
                    writes.Add(preparedCreate);
                    break;
                }
                case "com.atproto.repo.applyWrites#update":
                {
                    var update = (Update)write;
                    if (update.Collection == null || update.Value == null || update.Rkey == null)
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid update."));
                    }
                    var preparedUpdate = Prepare.PrepareUpdate(did, update.Collection, update.Rkey, null, update.Value, validate);
                    writes.Add(preparedUpdate);
                    break;
                }
                case "com.atproto.repo.applyWrites#delete":
                {
                    var delete = (Delete)write;
                    if (delete.Collection == null || delete.Rkey == null)
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail("Invalid delete."));
                    }
                    var preparedDelete = Prepare.PrepareDelete(did, delete.Collection, delete.Rkey, swapRecord != null ? Cid.FromString(swapRecord) : null);
                    writes.Add(preparedDelete);
                    break;
                }
                default:
                {
                    throw new XRPCError(new InvalidRequestErrorDetail($"Action not supported: {write.Type}"));
                }
            }
        }

        Cid? swapCommitCid = swapCommit != null ? Cid.FromString(swapCommit) : null;

        var writeArr = writes.ToArray();
        await using var db = _actorRepositoryProvider.Open(did);
        var commit = await db.Repo.ProcessWrites(writeArr, swapCommitCid);

        await _sequencer.SequenceCommit(did, commit, writeArr);
        await _accountRepository.UpdateRepoRoot(did, commit.Cid, commit.Rev);

        return (commit, writeArr);
    }

    public ATObject WriteToOutputResult(IPreparedWrite write)
    {
        return write switch
        {
            PreparedCreate create => new CreateResult(create.Uri, create.Cid.ToString(), create.ValidationStatus.ToString()),
            PreparedUpdate update => new UpdateResult(update.Uri, update.Cid.ToString(), update.ValidationStatus.ToString()),
            PreparedDelete delete => new DeleteResult(),
            _ => throw new Exception("Invalid write type.")
        };
    }
}