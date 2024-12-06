using System.Net.Http.Headers;
using atompds.Middleware;
using atompds.Pds.ActorStore.Db;
using atompds.Pds.Config;
using Crypto;
using FishyFlip.Lexicon.App.Bsky.Actor;
using Jose;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc;

[ApiController]
[Route("xrpc")]
public class AppViewProxyController : ControllerBase
{
    private readonly IBskyAppViewConfig _config;
    private readonly ILogger<AppViewProxyController> _logger;
    private readonly HttpClient _client;
    private readonly ActorStore _actorStore;
    public AppViewProxyController(IBskyAppViewConfig config, 
        ILogger<AppViewProxyController> logger, 
        HttpClient client,
        ActorStore actorStore)
    {
        _config = config;
        _logger = logger;
        _client = client;
        _actorStore = actorStore;
    }
    
    [HttpGet("app.bsky.actor.getPreferences")]
    [AccessStandard]
    public IActionResult AppViewProxy()
    {
        var auth = HttpContext.GetAuthOutput();

        return Ok(new GetPreferencesOutput([]));
    }
    
    [HttpPost("app.bsky.actor.putPreferences")]
    [AccessStandard]
    public IActionResult PutPreferences([FromBody] PutPreferencesInput request)
    {
        var auth = HttpContext.GetAuthOutput();
        return Ok();
    }
    
    // static appview proxy
    [HttpGet("app.bsky.actor.getProfile")]
    [AccessStandard]
    public async Task<IActionResult> GetProfile()
    {
        if (_config is not BskyAppViewConfig config)
        {
            throw new XRPCError(404);
        }
        
        var auth = HttpContext.GetAuthOutput();
        _logger.LogInformation($"Proxying request for {HttpContext.Request.Path} to {config.Url}");
        var reqNsid = ParseUrlNsid(HttpContext.Request.Path);
        var url = $"{config.Url}/xrpc/{reqNsid}";
        

        var signingKeyPair = _actorStore.KeyPair(auth.Credentials.Did, true);
        if (signingKeyPair is not IExportableKeyPair exportable)
        {
            throw new XRPCError(500);
        }
        
        var jwt = JWT.Encode(new Dictionary<string, object>()
        {
            ["iss"] = auth.Credentials.Did,
            ["aud"] = config.Did,
            ["lxm"] = reqNsid,
            ["exp"] = (int)DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()
        }, exportable.Export(), JwsAlgorithm.HS256);
        
        // if get
        if (HttpContext.Request.Method == "GET")
        {
            // add params if any
            url += HttpContext.Request.QueryString;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-bsky-topics",  HttpContext.Request.Headers["x-bsky-topics"].ToArray());
            request.Headers.Add("atproto-accept-labelers", HttpContext.Request.Headers["atproto-accept-labelers"].ToArray());
            var acceptLanguage = HttpContext.Request.Headers["Accept-Language"];
            if (acceptLanguage.Count > 0)
            {
                request.Headers.Add("Accept-Language", acceptLanguage.ToArray());
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        else
        {
            throw new XRPCError(405);
        }
    }

    private string ParseUrlNsid(string requestUrl)
    {
        // if doesn't start with /xrpc/ throw
        if (!requestUrl.StartsWith("/xrpc/"))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
        }
        
        var nsid = requestUrl[6..];
        // 0-9, A-Z, a-z
        // -, . <- allow only if previous char was 0-9, A-Z, a-z
        // / <- allow trailing slash if next char is end or ?
        // ?

        char currentChar;
        bool alphaNumRequired = true;
        int curr = 0;
        for (; curr < nsid.Length; curr++)
        {
            currentChar = nsid[curr];
            if (currentChar 
                is >= '0' and <= '9' 
                or >= 'A' and <= 'Z' 
                or >= 'a' and <= 'z')
            {
                alphaNumRequired = false;
            }
            else if (currentChar is '-' or '.')
            {
                if (alphaNumRequired)
                    throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
                alphaNumRequired = true;
            }
            else if (currentChar is '/')
            {
                if (curr == nsid.Length - 1 || nsid[curr + 1] == '?')
                    break;
                throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
            }
            else if (currentChar == '?')
            {
                break;
            }
            else
            {
                throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
            }
        }

        if (alphaNumRequired)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
        }
        
        if (curr < 2)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
        }
        
        return nsid[..curr];
    }
}