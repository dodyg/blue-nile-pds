using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ActorStore;
using atompds.Middleware;
using Config;
using Crypto;
using FishyFlip.Lexicon.App.Bsky.Actor;
using Identity;
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
    private readonly ActorRepository _actorRepository;
    private readonly IdResolver _idResolver;
    public AppViewProxyController(IBskyAppViewConfig config, 
        ILogger<AppViewProxyController> logger, 
        HttpClient client,
        ActorRepository actorRepository,
        IdResolver idResolver)
    {
        _config = config;
        _logger = logger;
        _client = client;
        _actorRepository = actorRepository;
        _idResolver = idResolver;
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
    
    [HttpPost("chat.bsky.actor.deleteAccount")]
    [AccessStandard]
    public IActionResult StubChatDeleteAccount()
    {
        var auth = HttpContext.GetAuthOutput();
        return Ok();
    }
    
    // static appview proxy
    [HttpGet("app.bsky.actor.getProfile")]
    [HttpGet("app.bsky.actor.getProfiles")]
    [HttpGet("app.bsky.actor.getSuggestions")]
    [HttpGet("app.bsky.actor.searchActorsTypeahead")]
    [HttpGet("app.bsky.labeler.getServices")]
    [HttpGet("app.bsky.notification.listNotifications")]
    [HttpPost("app.bsky.notification.updateSeen")]
    [HttpGet("app.bsky.graph.getList")]
    [HttpGet("app.bsky.graph.getLists")]
    [HttpGet("app.bsky.graph.getFollows")]
    [HttpGet("app.bsky.graph.getFollowers")]
    [HttpGet("app.bsky.graph.getStarterPack")]
    [HttpGet("app.bsky.graph.getSuggestedFollowsByActor")]
    [HttpGet("app.bsky.graph.getActorStarterPacks")]
    [HttpPost("app.bsky.graph.muteActor")]
    [HttpPost("app.bsky.graph.unmuteActor")]
    [HttpGet("app.bsky.feed.getTimeline")]
    [HttpGet("app.bsky.feed.getAuthorFeed")]
    [HttpGet("app.bsky.feed.getActorFeeds")]
    [HttpGet("app.bsky.feed.getFeed")]
    [HttpGet("app.bsky.feed.getListFeed")]
    [HttpGet("app.bsky.feed.getFeedGenerator")]
    [HttpGet("app.bsky.feed.getFeedGenerators")]
    [HttpGet("app.bsky.feed.getPostThread")]
    [HttpGet("app.bsky.feed.getPosts")]
    [HttpGet("app.bsky.feed.getLikes")]
    [HttpGet("app.bsky.feed.getActorLikes")]
    [HttpGet("app.bsky.unspecced.getPopularFeedGenerators")]
    [HttpGet("chat.bsky.convo.listConvos")]
    [AccessStandard]
    public async Task<IActionResult> Method()
    {
        try
        {
            return await Inner();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in AppViewProxyController");
            return StatusCode(500);
        }
    }
    
    private async Task<IActionResult> Inner()
    {
        if (_config is not BskyAppViewConfig config)
        {
            throw new XRPCError(404);
        }
        
        var auth = HttpContext.GetAuthOutput();
        _logger.LogInformation($"Proxying request for {HttpContext.Request.Path} to {config.Url}");
        var reqNsid = ParseUrlNsid(HttpContext.Request.Path);
        var url = $"{config.Url}/xrpc/{reqNsid}";
        

        var signingKeyPair = _actorRepository.KeyPair(auth.AccessCredentials.Did, true);
        if (signingKeyPair is not IExportableKeyPair exportable)
        {
            throw new XRPCError(500);
        }
        
        var jwt = CreateServiceJwt(new ServiceJwtPayload(
            auth.AccessCredentials.Did,
            config.Did,
            null,
            null,
            reqNsid,
            exportable
        ));
        //await AssertValidJwt(jwt, config.Did, reqNsid);
        
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
            
            //_logger.LogInformation("Response from proxy: " + content);
            
            return new ContentResult
            {
                Content = content,
                StatusCode = (int) response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString()
            };
        }
        else if (HttpContext.Request.Method == "POST")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-bsky-topics",  HttpContext.Request.Headers["x-bsky-topics"].ToArray());
            request.Headers.Add("atproto-accept-labelers", HttpContext.Request.Headers["atproto-accept-labelers"].ToArray());
            var acceptLanguage = HttpContext.Request.Headers["Accept-Language"];
            if (acceptLanguage.Count > 0)
            {
                request.Headers.Add("Accept-Language", acceptLanguage.ToArray());
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            
            // add body if any
            if (HttpContext.Request.ContentLength > 0)
            {
                var body = await HttpContext.Request.BodyReader.ReadAsync();
                request.Content = new ByteArrayContent(body.Buffer.ToArray());
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(HttpContext.Request.ContentType);
            }
            
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            //_logger.LogInformation("Response from proxy: " + content);
            
            return new ContentResult
            {
                Content = content,
                StatusCode = (int) response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString()
            };
        }
        else
        {
            throw new XRPCError(405);
        }
    }
    
    private record ServiceJwtPayload(string iss, string? aud, long? iat, long? exp, string? lxm, IKeyPair KeyPair);
    private string CreateServiceJwt(ServiceJwtPayload payload)
    {
        var iat = payload.iat ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp = payload.exp ?? DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var jti = Crypto.Utils.RandomHexString(16);
        var header = new
        {
            typ = "JWT",
            alg = payload.KeyPair.JwtAlg
        };
        var values = new Dictionary<string, object?>
        {
            ["iat"] = iat,
            ["iss"] = payload.iss,
            ["aud"] = payload.aud,
            ["exp"] = exp,
            ["lxm"] = payload.lxm,
            ["jti"] = jti
        };
        var pl = values
            .Where(kv => kv.Value != null)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var toSignStr = $"{Base64Url.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)))}." +
                        $"{Base64Url.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(pl)))}";
        var toSign = Encoding.UTF8.GetBytes(toSignStr);
        var sig = payload.KeyPair.Sign(toSign);
        return $"{toSignStr}.{Base64Url.Encode(sig)}";
    }

    private async Task AssertValidJwt(string jwtStr, string? ownDid, string? lxm)
    {
        Dictionary<string, string> parseHeader(string b64)
        {
            var header = Encoding.UTF8.GetString(Base64Url.Decode(b64));
            return JsonSerializer.Deserialize<Dictionary<string, string>>(header)!;
        }
        
        Dictionary<string, string?> parsePayload(string b64)
        {
            var blob = Base64Url.Decode(b64);
            var payload = Encoding.UTF8.GetString(blob);
            var obj = JsonSerializer.Deserialize<Dictionary<string, object?>>(payload)!;
            if (!obj.TryGetValue("iss", out var iss) ||
                !obj.TryGetValue("aud", out var aud) ||
                !obj.TryGetValue("exp", out var exp))
            {
                throw new XRPCError(new AuthRequiredErrorDetail("missing required fields"));
            }
            
            return obj.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());
        }
        
        var parts = jwtStr.Split('.');
        if (parts.Length != 3)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("poorly formatted jwt"));
        }
        
        var header = parseHeader(parts[0]);
        if (header.TryGetValue("typ", out var typ) && typ is "at+jwt" or "refresh+jwt" or "dpop+jwt")
        {
            throw new XRPCError(new AuthRequiredErrorDetail($"invalid jwt type: {typ}"));
        }
        
        var payload = parsePayload(parts[1]);
        var sig = parts[2];
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp = long.Parse(payload["exp"]!);
        if (exp < now)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("jwt expired"));
        }
        
        if (ownDid != null && payload["aud"] != ownDid)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("invalid audience"));
        }

        if (lxm != null)
        {
            if (!payload.TryGetValue("lxm", out var payloadLxm))
            {
                throw new XRPCError(new AuthRequiredErrorDetail("missing lxm"));
            }
            else
            {
                if (payloadLxm != lxm)
                {
                    throw new XRPCError(new AuthRequiredErrorDetail("invalid lxm"));
                }
            }
        }
        
        var msgBytes = Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]);
        var sigBytes = Base64Url.Decode(sig);
        var signingKey = await _idResolver.DidResolver.ResolveAtproto(payload["iss"], true);

        var alg = header["alg"];

        var valid = Verify.VerifySignature(signingKey.SigningKey, msgBytes, sigBytes, null, alg);
        if (!valid)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("invalid signature"));
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