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
    private readonly ILogger<ApplyWritesController> _logger;
    private readonly AccountManager.AccountRepository _accountRepository;
    private readonly IdentityConfig _identityConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly InvitesConfig _invitesConfig;
    private readonly HttpClient _httpClient;
    private readonly HandleManager _handle;
    private readonly ActorRepository _actorRepository;
    private readonly IdResolver _idResolver;
    private readonly SecretsConfig _secretsConfig;
    private readonly SequencerRepository _sequencer;
    private readonly PlcClient _plcClient;

    public ApplyWritesController(ILogger<ApplyWritesController> logger,
        AccountManager.AccountRepository accountRepository,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        InvitesConfig invitesConfig,
        HttpClient httpClient,
        HandleManager handle,
        ActorRepository actorRepository,
        IdResolver idResolver,
        SecretsConfig secretsConfig,
        SequencerRepository sequencer,
        PlcClient plcClient)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _invitesConfig = invitesConfig;
        _httpClient = httpClient;
        _handle = handle;
        _actorRepository = actorRepository;
        _idResolver = idResolver;
        _secretsConfig = secretsConfig;
        _sequencer = sequencer;
        _plcClient = plcClient;
    }
    
    [HttpPost("com.atproto.repo.applyWrites")]
    [AccessStandard(checkTakenDown: true, checkDeactivated: true)]
    public async Task<IActionResult> ApplyWrites([FromBody] ApplyWritesInput tx)
    {
        var repo = tx.Repo;
        var validate = tx.Validate;
        var swapCommit = tx.SwapCommit;

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
        else if (account.DeactivatedAt != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account is deactivated."));
        }

        var did = account.Did;
        if (did != auth.AccessCredentials.Did)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Invalid did."));
        }

        if (tx.Writes == null || tx.Writes.Count > 200)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid writes."));
        }

        var writes = new List<IPreparedWrite>();
        foreach (var write in tx.Writes)
        {
            switch (write.Type)
            {
                case "com.atproto.repo.applyWrites#create":
                {
                    var create = (Create)write;
                    if (create.Collection == null || create.Value == null) throw new XRPCError(new InvalidRequestErrorDetail("Invalid create."));
                    var preparedCreate = Prepare.PrepareCreate(did, create.Collection, create.Rkey, null, create.Value, validate);
                    writes.Add(preparedCreate);
                    break;
                }
                case "com.atproto.repo.applyWrites#update":
                {
                    var update = (Update)write;
                    if (update.Collection == null || update.Value == null || update.Rkey == null) throw new XRPCError(new InvalidRequestErrorDetail("Invalid update."));
                    var preparedUpdate = Prepare.PrepareUpdate(did, update.Collection, update.Rkey, null, update.Value, validate);
                    writes.Add(preparedUpdate);
                    break;
                }
                case "com.atproto.repo.applyWrites#delete":
                {
                    var delete = (Delete)write;
                    if (delete.Collection == null || delete.Rkey == null) throw new XRPCError(new InvalidRequestErrorDetail("Invalid delete."));
                    var preparedDelete = Prepare.PrepareDelete(did, delete.Collection, delete.Rkey, null);
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
        CommitData commit;
        await using (var db = _actorRepository.Open(did))
        {
            var repoRepository = _actorRepository.GetRepo(did, db);
            commit = await repoRepository.ProcessWrites(writeArr, swapCommitCid);
        }

        await _sequencer.SequenceCommit(did, commit, writeArr);
        await _accountRepository.UpdateRepoRoot(did, commit.Cid, commit.Rev);
        
        return Ok(new ApplyWritesOutput(new CommitMeta(commit.Cid.ToString(), commit.Rev), writeArr.Select(WriteToOutputResult).ToList()));
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