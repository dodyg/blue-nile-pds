using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
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
public class RefreshSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly IdentityConfig _identityConfig;
    private readonly IdResolver _idResolver;
    private readonly ILogger<RefreshSessionController> _logger;

    public RefreshSessionController(AccountRepository accountRepository,
        IdentityConfig identityConfig,
        IdResolver idResolver,
        ILogger<RefreshSessionController> logger)
    {
        _accountRepository = accountRepository;
        _identityConfig = identityConfig;
        _idResolver = idResolver;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.refreshSession")]
    [Refresh]
    public async Task<IActionResult> RefreshSession()
    {
        var auth = HttpContext.GetRefreshOutput();
        var did = auth.RefreshCredentials.Did;
        var tokenId = auth.RefreshCredentials.TokenId;

        var user = await _accountRepository.GetAccount(did, new AvailabilityFlags(true, true));
        if (user == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not find user info for account: {did}"));
        }

        if (user.SoftDeleted)
        {
            throw new XRPCError(new AccountTakenDownErrorDetail("Account has been taken down"));
        }

        var didDocTask = DidDocForSession(did);
        var rotateTask = _accountRepository.RotateRefreshToken(tokenId);
        await Task.WhenAll(didDocTask, rotateTask);

        var didDoc = didDocTask.Result;
        var rotated = rotateTask.Result;

        if (rotated == null)
        {
            throw new XRPCError(new ExpiredTokenErrorDetail("Token has been revoked"));
        }

        var (active, status) = AccountStore.FormatAccountStatus(user);

        return Ok(new RefreshSessionOutput(
            rotated.Value.AccessJwt,
            rotated.Value.RefreshJwt,
            new ATHandle(user.Handle ?? Constants.INVALID_HANDLE),
            new ATDid(user.Did),
            didDoc?.ToDidDoc(),
            active,
            status.ToString()));
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
