using AccountManager;
using AccountManager.Db;
using atompds.Utils;
using CarpaNet;
using CommonWeb;
using ComAtproto.Server;
using Config;
using Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> CreateSessionAsync([FromBody] CreateSessionInput request)
    {
        if (request.Identifier == null || request.Password == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Identifier and password are required"));
        }

        var login = await _accountRepository.LoginAsync(request.Identifier, request.Password, request.AllowTakendown == true);
        var creds = await _accountRepository.CreateSessionAsync(login.Account.Did, login.AppPasswordName, login.AppPasswordScope);
        var didDoc = await DidDocForSessionAsync(login.Account.Did);
        var (active, status) = AccountStore.FormatAccountStatus(login.Account);

        return Ok(new CreateSessionOutput
        {
            AccessJwt = creds.AccessJwt,
            RefreshJwt = creds.RefreshJwt,
            Handle = new ATHandle(login.Account.Handle ?? Constants.INVALID_HANDLE),
            Did = new ATDid(login.Account.Did),
            DidDoc = didDoc?.ToJsonElement(),
            Email = login.Account.Email,
            EmailConfirmed = login.Account.EmailConfirmedAt != null,
            EmailAuthFactor = null,
            Active = active,
            Status = status.ToString()
        });
    }

    private async Task<DidDocument?> DidDocForSessionAsync(string did, bool forceRefresh = false)
    {
        if (!_identityConfig.EnableDidDocWithSession)
        {
            return null;
        }
        return await SafeResolveDidDocAsync(did, forceRefresh);
    }

    private async Task<DidDocument?> SafeResolveDidDocAsync(string did, bool forceRefresh = false)
    {
        try
        {
            var didDoc = await _idResolver.DidResolver.ResolveAsync(did, forceRefresh);
            return didDoc;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve did doc: {did}", did);
            return null;
        }
    }
}
