using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
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
public class GetSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly IdentityConfig _identityConfig;
    private readonly IdResolver _idResolver;
    private readonly ILogger<GetSessionController> _logger;

    public GetSessionController(
        AccountRepository accountRepository,
        IdentityConfig identityConfig,
        IdResolver idResolver,
        ILogger<GetSessionController> logger)
    {
        _accountRepository = accountRepository;
        _identityConfig = identityConfig;
        _idResolver = idResolver;
        _logger = logger;
    }

    [HttpGet("com.atproto.server.getSession")]
    [AccessStandard]
    public async Task<IActionResult> GetSessionAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not find account: {did}"));
        }

        var didDoc = await DidDocForSessionAsync(did);
        var (active, status) = AccountStore.FormatAccountStatus(account);

        return Ok(new GetSessionOutput
        {
            Handle = new ATHandle(account.Handle ?? Constants.INVALID_HANDLE),
            Did = new ATDid(account.Did),
            DidDoc = didDoc?.ToJsonElement(),
            Email = account.Email,
            EmailConfirmed = account.EmailConfirmedAt != null,
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
