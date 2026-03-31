using AccountManager;
using AccountManager.Db;
using atompds.Utils;
using CarpaNet;
using CommonWeb;
using ComAtproto.Server;
using Config;
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
        var (active, status) = AccountStore.FormatAccountStatus(login);

        return Ok(new CreateSessionOutput
        {
            AccessJwt = creds.AccessJwt,
            RefreshJwt = creds.RefreshJwt,
            Handle = new ATHandle(login.Handle ?? Constants.INVALID_HANDLE),
            Did = new ATDid(login.Did),
            DidDoc = didDoc?.ToJsonElement(),
            Email = login.Email,
            EmailConfirmed = login.EmailConfirmedAt != null,
            EmailAuthFactor = null,
            Active = active,
            Status = status.ToString()
        });
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
