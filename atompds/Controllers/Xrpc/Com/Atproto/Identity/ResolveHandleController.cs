using System.Text.Json;
using Config;
using FishyFlip.Lexicon.Com.Atproto.Identity;
using FishyFlip.Models;
using Handle;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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
    private readonly HttpClient _client;
    private readonly IBskyAppViewConfig _appViewConfig;
    public ResolveHandleController(
        AccountManager.AccountRepository accountRepository,
        HandleManager handle, 
        IdentityConfig identityConfig,
        HttpClient client,
        IBskyAppViewConfig appViewConfig,
        ILogger<ResolveHandleController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
        _handle = handle;
        _identityConfig = identityConfig;
        _client = client;
        _appViewConfig = appViewConfig;
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
            // disabling this error since I have handles registered with the appview
            // if (_identityConfig.ServiceHandleDomains.Any(x => handle.EndsWith(x) || handle == x[1..]))
            // {
            //     throw new XRPCError(new InvalidRequestErrorDetail("Unable to resolve handle"));
            // }
        }

        if (did == null)
        {
            // TODO: if identity is not from our server, we should direct the appview to attempt to resolve the handle
            did = await TryResolveFromAppView(handle);
        }

        if (did == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Unable to resolve handle"));
        }

        return Ok(new ResolveHandleOutput(new ATDid(did)));
    }
    
    private async Task<string?> TryResolveFromAppView(string handle)
    {
        if (_appViewConfig is BskyAppViewConfig appViewConfig)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{appViewConfig.Url}/xrpc/com.atproto.identity.resolveHandle?handle={handle}");
            var response = await _client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jobj = JsonDocument.Parse(content).RootElement;
                if (jobj.TryGetProperty("did", out var did))
                {
                    return did.GetString();
                }
            }
        }
        
        return null;
    }
}