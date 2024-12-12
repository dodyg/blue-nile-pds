using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Utils;
using CommonWeb;
using FishyFlip.Lexicon.Com.Atproto.Repo;
using FishyFlip.Models;
using Identity;
using Microsoft.AspNetCore.Mvc;
using Xrpc;
using DidDoc = CommonWeb.DidDoc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Repo;

[ApiController]
[Route("xrpc")]
public class DescribeRepoController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly IdResolver _idResolver;
    private readonly ActorRepository _actorRepository;
    private readonly ILogger<DescribeRepoController> _logger;
    public DescribeRepoController(AccountRepository accountRepository, 
        IdResolver idResolver, 
        ActorRepository actorRepository,
        ILogger<DescribeRepoController> logger)
    {
        _accountRepository = accountRepository;
        _idResolver = idResolver;
        _actorRepository = actorRepository;
        _logger = logger;
    }
    
    [HttpGet("com.atproto.repo.describeRepo")]
    public async Task<IActionResult> DescribeRepo(
        [FromQuery] string repo)
    {
        var account = await AssertRepoAvailability(repo);

        DidDocument didDoc;
        try
        {
            didDoc = await _idResolver.DidResolver.EnsureResolve(account.Did);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve DID: {Did}", account.Did);
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not resolve DID: {account.Did}"));
        }
        
        var handle = DidDoc.GetHandle(didDoc);
        var handleIsCorrect = handle == account.Handle;
        
        await using var actorDb = _actorRepository.Open(account.Did);
        var repoRepository = _actorRepository.GetRepo(account.Did, actorDb);
        var collections = await repoRepository.GetCollections();
        
        return Ok(new DescribeRepoOutput(new ATHandle(handle), new ATDid(account.Did), didDoc.ToDidDoc(), collections.ToList(), handleIsCorrect));
    }

    private async Task<ActorAccount> AssertRepoAvailability(string did, bool isAdminOrSelf = false)
    {
        var account = await _accountRepository.GetAccount(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("RepoNotFound", $"Could not find repo for DID: {did}"));
        }

        if (isAdminOrSelf)
        {
            return account;
        }
        
        if (account.TakedownRef != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("RepoTakedown", $"Repo has been takendown: {did}"));
        }

        if (account.DeactivatedAt != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("RepoDeactivated", $"Repo has been deactivated: {did}"));
        }
        
        return account;
    }
}