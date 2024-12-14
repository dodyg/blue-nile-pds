using AccountManager;
using AccountManager.Db;
using atompds.Utils;
using CommonWeb;
using Config;
using FishyFlip.Lexicon.Com.Atproto.Server;
using FishyFlip.Models;
using Identity;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class CreateSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly IdentityConfig _identityConfig;
    private readonly IdResolver _idResolver;
    private readonly ILogger<CreateSessionController> _logger;

    public CreateSessionController(AccountRepository accountRepository,
        IdentityConfig identityConfig,
        IdResolver idResolver,
        ILogger<CreateSessionController> logger)
    {
        _accountRepository = accountRepository;
        _identityConfig = identityConfig;
        _idResolver = idResolver;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.createSession")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionInput request)
    {
        if (request.Identifier == null || request.Password == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Identifier and password are required"));
        }

        var login = await _accountRepository.Login(request.Identifier, request.Password);
        var creds = await _accountRepository.CreateSession(login.Did);
        var didDoc = await DidDocForSession(login.Did);
        var (active, status) = FormatAccountStatus(login);

        return Ok(new CreateSessionOutput(creds.AccessJwt,
            creds.RefreshJwt,
            new ATHandle(login.Handle ?? Constants.INVALID_HANDLE),
            new ATDid(login.Did),
            didDoc?.ToDidDoc(),
            login.Email,
            login.EmailConfirmedAt != null,
            null,
            active,
            status.ToString()));
    }

    private (bool Active, AccountStore.AccountStatus Status) FormatAccountStatus(ActorAccount? account)
    {
        if (account == null)
        {
            return (false, AccountStore.AccountStatus.Deleted);
        }

        if (account.TakedownRef != null)
        {
            return (false, AccountStore.AccountStatus.Takendown);
        }

        if (account.DeactivatedAt != null)
        {
            return (false, AccountStore.AccountStatus.Deactivated);
        }

        return (true, AccountStore.AccountStatus.Active);
    }

    private async Task<DidDocument?> DidDocForSession(string did, bool forceRefresh = false)
    {
        if (!_identityConfig.EnableDidDocWithSession)
        {
            return null;
        }
        return await SafeResolveDidDoc(did, forceRefresh);
    }

    private async Task<DidDocument?> SafeResolveDidDoc(string did, bool forceRefresh = false)
    {
        try
        {
            var didDoc = await _idResolver.DidResolver.Resolve(did, forceRefresh);
            return didDoc;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve did doc: {did}", did);
            return null;
        }
    }
}