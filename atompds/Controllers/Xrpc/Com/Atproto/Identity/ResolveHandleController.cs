using Config;
using FishyFlip.Lexicon.Com.Atproto.Identity;
using FishyFlip.Models;
using Handle;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Identity;

[ApiController]
[Route("xrpc")]
public class ResolveHandleController : ControllerBase
{
    private readonly AccountManager.AccountRepository _accountRepository;
    private readonly ILogger<ResolveHandleController> _logger;
    private readonly HandleManager _handle;
    private readonly IdentityConfig _identityConfig;
    public ResolveHandleController(
        AccountManager.AccountRepository accountRepository,
        HandleManager handle, 
        IdentityConfig identityConfig,
        ILogger<ResolveHandleController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
        _handle = handle;
        _identityConfig = identityConfig;
    }

    [HttpGet("com.atproto.identity.resolveHandle")]
    public async Task<IActionResult> ResolveHandle([FromQuery] string handle)
    {
        _logger.LogInformation("Resolving handle {Handle}", handle);
        handle = _handle.NormalizeAndEnsureValidHandle(handle);

        string? did = null;
        var user = await _accountRepository.GetAccount(handle);
        if (user != null)
        {
            did = user.Did;
        }
        else
        {
            if (_identityConfig.ServiceHandleDomains.Any(x => handle.EndsWith(x) || handle == x[1..]))
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Unable to resolve handle"));
            }
        }

        if (did == null)
        {
            // TODO: if identity is not from out server, we should direct the appview to attempt to resolve the handle
            // did = await tryResolveFromAppView(ctx.appViewAgent, handle);
        }

        if (did == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Unable to resolve handle"));
        }

        return Ok(new ResolveHandleOutput(new ATDid(did)));
    }
}