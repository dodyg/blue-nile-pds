using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AppBsky.Actor;
using ActorStore;
using atompds.Middleware;
using atompds.Services;
using Config;
using CommonWeb;
using Crypto;
using Identity;
using Jose;
using Xrpc;

namespace atompds.Endpoints.Xrpc;

public static class AppViewProxyEndpoints
{
    public static RouteGroupBuilder MapAppViewProxyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("app.bsky.actor.getPreferences", GetPreferences).WithMetadata(new AccessStandardAttribute());
        group.MapPost("app.bsky.actor.putPreferences", PutPreferences).WithMetadata(new AccessStandardAttribute());
        group.MapPost("chat.bsky.actor.deleteAccount", StubChatDeleteAccount).WithMetadata(new AccessStandardAttribute());
        group.MapPost("app.bsky.notification.registerPush", RegisterPushAsync);

        // Static proxy routes
        group.MapGet("app.bsky.actor.getProfile", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.actor.getProfiles", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.actor.getSuggestions", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.actor.searchActorsTypeahead", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.labeler.getServices", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.notification.listNotifications", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapPost("app.bsky.notification.updateSeen", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.graph.getList", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.graph.getLists", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.graph.getFollows", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.graph.getFollowers", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.graph.getStarterPack", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.graph.getSuggestedFollowsByActor", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.graph.getActorStarterPacks", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapPost("app.bsky.graph.muteActor", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapPost("app.bsky.graph.unmuteActor", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getTimeline", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getAuthorFeed", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getActorFeeds", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getFeed", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getListFeed", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getFeedGenerator", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getFeedGenerators", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getPostThread", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getPosts", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getLikes", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getActorLikes", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.unspecced.getPopularFeedGenerators", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("chat.bsky.convo.listConvos", ProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapGet("app.bsky.feed.getRepostedBy", ProxyAsync).WithMetadata(new AccessStandardAttribute());

        // Catchall
        group.MapGet("{nsid}", CatchallProxyAsync).WithMetadata(new AccessStandardAttribute());
        group.MapPost("{nsid}", CatchallProxyAsync).WithMetadata(new AccessStandardAttribute());

        return group;
    }

    private static IResult GetPreferences(HttpContext context)
    {
        var auth = context.GetAuthOutput();
        return Results.Ok(new GetPreferencesOutput { Preferences = [] });
    }

    private static IResult PutPreferences(HttpContext context)
    {
        var auth = context.GetAuthOutput();
        return Results.Ok();
    }

    private static IResult StubChatDeleteAccount(HttpContext context)
    {
        var auth = context.GetAuthOutput();
        return Results.Ok();
    }

    private static async Task<IResult> RegisterPushAsync(
        HttpContext context,
        RegisterPushRequest request,
        IBskyAppViewConfig config,
        ActorRepositoryProvider actorRepositoryProvider,
        HttpClient client,
        IdResolver idResolver,
        ServiceJwtBuilder serviceJwtBuilder,
        WriteSnapshotCache writeSnapshotCache,
        ILogger<Program> logger)
    {
        try
        {
            var authVerifier = context.RequestServices.GetRequiredService<AuthVerifier>();
            var auth = await authVerifier.ValidateAccessTokenAsync(context,
            [
                AuthVerifier.ScopeMap[AuthVerifier.AuthScope.Access],
                AuthVerifier.ScopeMap[AuthVerifier.AuthScope.AppPass],
                AuthVerifier.ScopeMap[AuthVerifier.AuthScope.AppPassPrivileged],
                AuthVerifier.ScopeMap[AuthVerifier.AuthScope.SignupQueued]
            ]);

            return await InnerRegisterPushAsync(context, auth, request, config, client, idResolver, serviceJwtBuilder, logger);
        }
        catch (XRPCError)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in AppViewProxy RegisterPush");
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> ProxyAsync(
        HttpContext context,
        IBskyAppViewConfig config,
        ActorRepositoryProvider actorRepositoryProvider,
        HttpClient client,
        IdResolver idResolver,
        ServiceJwtBuilder serviceJwtBuilder,
        WriteSnapshotCache writeSnapshotCache,
        ILogger<Program> logger)
    {
        try
        {
            return await InnerAsync(context, config, actorRepositoryProvider, client, idResolver, serviceJwtBuilder, writeSnapshotCache, logger);
        }
        catch (XRPCError)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in AppViewProxy");
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> CatchallProxyAsync(
        string nsid,
        HttpContext context,
        IBskyAppViewConfig config,
        ActorRepositoryProvider actorRepositoryProvider,
        HttpClient client,
        IdResolver idResolver,
        ServiceJwtBuilder serviceJwtBuilder,
        WriteSnapshotCache writeSnapshotCache,
        ILogger<Program> logger)
    {
        if (!nsid.StartsWith("app.bsky.") && !nsid.StartsWith("chat.bsky.") && !nsid.StartsWith("com.atproto.moderation."))
        {
            return Results.NotFound();
        }

        try
        {
            return await InnerAsync(context, config, actorRepositoryProvider, client, idResolver, serviceJwtBuilder, writeSnapshotCache, logger);
        }
        catch (XRPCError e) when (e.Status == ResponseType.XRPCNotSupported)
        {
            return Results.NotFound();
        }
        catch (XRPCError)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in catchall proxy for {nsid}", nsid);
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> InnerAsync(
        HttpContext context,
        IBskyAppViewConfig config,
        ActorRepositoryProvider actorRepositoryProvider,
        HttpClient client,
        IdResolver idResolver,
        ServiceJwtBuilder serviceJwtBuilder,
        WriteSnapshotCache writeSnapshotCache,
        ILogger logger)
    {
        var auth = context.GetAuthOutput();
        var reqNsid = ParseUrlNsid(context.Request.Path);
        var proxyTarget = await ResolveProxyTargetAsync(context, config, idResolver);
        var url = $"{proxyTarget.Url}/xrpc/{reqNsid}";

        var signingKeyPair = actorRepositoryProvider.KeyPair(auth.AccessCredentials.Did, true);
        if (signingKeyPair is not IExportableKeyPair)
        {
            throw new XRPCError(500);
        }

        var jwt = serviceJwtBuilder.CreateServiceJwt(
            auth.AccessCredentials.Did,
            proxyTarget.Did,
            reqNsid);

        if (context.Request.Method == "GET")
        {
            url += context.Request.QueryString;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-bsky-topics", context.Request.Headers["x-bsky-topics"].ToArray());
            request.Headers.Add("atproto-accept-labelers", context.Request.Headers["atproto-accept-labelers"].ToArray());
            var acceptLanguage = context.Request.Headers["Accept-Language"];
            if (acceptLanguage.Count > 0)
            {
                request.Headers.Add("Accept-Language", acceptLanguage.ToArray());
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            content = PatchReadAfterWriteResponse(reqNsid, auth.AccessCredentials.Did, response, content, writeSnapshotCache);

            logger.LogDebug("[PROXY][{status}] {path} via {serviceDid}", response.StatusCode, url, proxyTarget.Did);

            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
            await context.Response.WriteAsync(content);
            return Results.Empty;
        }
        if (context.Request.Method == "POST")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-bsky-topics", context.Request.Headers["x-bsky-topics"].ToArray());
            request.Headers.Add("atproto-accept-labelers", context.Request.Headers["atproto-accept-labelers"].ToArray());
            var acceptLanguage = context.Request.Headers["Accept-Language"];
            if (acceptLanguage.Count > 0)
            {
                request.Headers.Add("Accept-Language", acceptLanguage.ToArray());
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            if (context.Request.ContentLength > 0)
            {
                var body = await context.Request.BodyReader.ReadAsync();
                request.Content = new ByteArrayContent(body.Buffer.ToArray());
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                {
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(context.Request.ContentType);
                }
            }

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            logger.LogDebug("[PROXY][{status}] {path} via {serviceDid}", response.StatusCode, url, proxyTarget.Did);

            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
            await context.Response.WriteAsync(content);
            return Results.Empty;
        }
        throw new XRPCError(405);
    }

    private static async Task<IResult> InnerRegisterPushAsync(
        HttpContext context,
        AuthVerifier.AccessOutput auth,
        RegisterPushRequest request,
        IBskyAppViewConfig config,
        HttpClient client,
        IdResolver idResolver,
        ServiceJwtBuilder serviceJwtBuilder,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceDid))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("serviceDid is required"));
        }

        const string reqNsid = "app.bsky.notification.registerPush";
        var proxyTarget = await ResolveNotificationTargetAsync(request.ServiceDid, config, idResolver);
        var url = $"{proxyTarget.Url}/xrpc/{reqNsid}";
        var jwt = serviceJwtBuilder.CreateServiceJwt(auth.AccessCredentials.Did, proxyTarget.Did, reqNsid);

        using var proxyRequest = new HttpRequestMessage(HttpMethod.Post, url);
        proxyRequest.Headers.Add("x-bsky-topics", context.Request.Headers["x-bsky-topics"].ToArray());
        proxyRequest.Headers.Add("atproto-accept-labelers", context.Request.Headers["atproto-accept-labelers"].ToArray());
        var acceptLanguage = context.Request.Headers["Accept-Language"];
        if (acceptLanguage.Count > 0)
        {
            proxyRequest.Headers.Add("Accept-Language", acceptLanguage.ToArray());
        }

        proxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        proxyRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(proxyRequest);
        var content = await response.Content.ReadAsStringAsync();

        logger.LogDebug("[PROXY][{status}] {path} via {serviceDid}", response.StatusCode, url, proxyTarget.Did);

        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
        await context.Response.WriteAsync(content);
        return Results.Empty;
    }

    private static string PatchReadAfterWriteResponse(string reqNsid, string did, HttpResponseMessage response, string content, WriteSnapshotCache writeSnapshotCache)
    {
        if (!response.IsSuccessStatusCode || !ShouldPatchReadAfterWrite(reqNsid) || string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            return content;
        }

        if (root == null || !PatchNode(root, did, writeSnapshotCache))
        {
            return content;
        }

        return root.ToJsonString();
    }

    private static bool PatchNode(JsonNode? node, string did, WriteSnapshotCache writeSnapshotCache)
    {
        if (node == null) return false;

        var modified = false;
        switch (node)
        {
            case JsonObject obj:
                modified |= PatchUriSnapshot(obj, did, writeSnapshotCache);
                modified |= PatchProfileSnapshot(obj, did, writeSnapshotCache);
                foreach (var child in obj.ToArray())
                {
                    modified |= PatchNode(child.Value, did, writeSnapshotCache);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    modified |= PatchNode(item, did, writeSnapshotCache);
                }
                break;
        }

        return modified;
    }

    private static bool PatchUriSnapshot(JsonObject obj, string did, WriteSnapshotCache writeSnapshotCache)
    {
        if (!obj.TryGetPropertyValue("uri", out var uriNode) || uriNode is not JsonValue uriValue)
            return false;

        if (!uriValue.TryGetValue<string>(out var uri) ||
            !TryParseAtUri(uri, out var uriDid, out var collection, out var rkey) ||
            !string.Equals(uriDid, did, StringComparison.Ordinal))
        {
            return false;
        }

        var snapshot = writeSnapshotCache.GetSnapshot(uriDid, collection, rkey);
        if (snapshot == null) return false;

        JsonNode? recordNode;
        try
        {
            recordNode = JsonNode.Parse(snapshot.RecordJson);
        }
        catch (JsonException)
        {
            return false;
        }

        var modified = false;
        if (obj.ContainsKey("cid")) { obj["cid"] = snapshot.Cid; modified = true; }
        if (obj.ContainsKey("record")) { obj["record"] = recordNode?.DeepClone(); modified = true; }
        if (obj.ContainsKey("value")) { obj["value"] = recordNode?.DeepClone(); modified = true; }
        if (recordNode is JsonObject recordObject) modified |= PromoteKnownViewFields(obj, recordObject);

        return modified;
    }

    private static bool PatchProfileSnapshot(JsonObject obj, string did, WriteSnapshotCache writeSnapshotCache)
    {
        if (!obj.TryGetPropertyValue("did", out var didNode) ||
            didNode is not JsonValue didValue ||
            !didValue.TryGetValue<string>(out var profileDid) ||
            !string.Equals(profileDid, did, StringComparison.Ordinal))
        {
            return false;
        }

        var snapshot = writeSnapshotCache.GetSnapshot(did, "app.bsky.actor.profile", "self");
        if (snapshot == null) return false;

        JsonNode? recordNode;
        try
        {
            recordNode = JsonNode.Parse(snapshot.RecordJson);
        }
        catch (JsonException)
        {
            return false;
        }

        return recordNode is JsonObject recordObject && PromoteKnownViewFields(obj, recordObject);
    }

    private static bool PromoteKnownViewFields(JsonObject target, JsonObject source)
    {
        var modified = false;
        foreach (var field in new[] { "displayName", "description", "name" })
        {
            if (source.TryGetPropertyValue(field, out var value))
            {
                target[field] = value?.DeepClone();
                modified = true;
            }
        }
        return modified;
    }

    private static bool TryParseAtUri(string uri, out string did, out string collection, out string rkey)
    {
        did = string.Empty;
        collection = string.Empty;
        rkey = string.Empty;

        if (!uri.StartsWith("at://", StringComparison.Ordinal)) return false;

        var segments = uri[5..].Split('/', 3, StringSplitOptions.None);
        if (segments.Length != 3 || string.IsNullOrWhiteSpace(segments[0]) ||
            string.IsNullOrWhiteSpace(segments[1]) || string.IsNullOrWhiteSpace(segments[2]))
        {
            return false;
        }

        did = segments[0];
        collection = segments[1];
        rkey = segments[2];
        return true;
    }

    private static bool ShouldPatchReadAfterWrite(string reqNsid) => reqNsid switch
    {
        "app.bsky.actor.getProfile" => true,
        "app.bsky.actor.getProfiles" => true,
        "app.bsky.feed.getTimeline" => true,
        "app.bsky.feed.getAuthorFeed" => true,
        "app.bsky.feed.getActorFeeds" => true,
        "app.bsky.feed.getFeed" => true,
        "app.bsky.feed.getListFeed" => true,
        "app.bsky.feed.getFeedGenerator" => true,
        "app.bsky.feed.getFeedGenerators" => true,
        "app.bsky.feed.getPostThread" => true,
        "app.bsky.feed.getPosts" => true,
        "app.bsky.feed.getLikes" => true,
        "app.bsky.feed.getActorLikes" => true,
        "app.bsky.feed.getRepostedBy" => true,
        "app.bsky.graph.getList" => true,
        "app.bsky.graph.getLists" => true,
        "app.bsky.graph.getStarterPack" => true,
        "app.bsky.graph.getActorStarterPacks" => true,
        "app.bsky.unspecced.getPopularFeedGenerators" => true,
        _ => false
    };

    private static async Task<ProxyServiceDestination> ResolveProxyTargetAsync(HttpContext context, IBskyAppViewConfig config, IdResolver idResolver)
    {
        var proxyHeader = context.Request.Headers["atproto-proxy"];
        if (proxyHeader.Count == 0)
        {
            if (config is not BskyAppViewConfig bskyConfig)
                throw new XRPCError(404);
            return new ProxyServiceDestination(bskyConfig.Did, NormalizeServiceUrl(bskyConfig.Url));
        }

        if (proxyHeader.Count != 1 || string.IsNullOrWhiteSpace(proxyHeader[0]))
            throw new XRPCError(new InvalidRequestErrorDetail("invalid atproto-proxy header"));

        var (serviceDid, serviceId) = ParseAtprotoProxyHeader(proxyHeader[0]!);

        if (config is BskyAppViewConfig appViewConfig &&
            string.Equals(serviceDid, appViewConfig.Did, StringComparison.Ordinal) &&
            string.Equals(serviceId, "bsky_appview", StringComparison.Ordinal))
        {
            return new ProxyServiceDestination(serviceDid, NormalizeServiceUrl(appViewConfig.Url));
        }

        var didDoc = await ResolveProxyDidDocumentAsync(serviceDid, idResolver);
        var endpoint = DidDoc.GetServiceEndpoint(didDoc, serviceId, null);
        if (endpoint == null)
            throw new XRPCError(new InvalidRequestErrorDetail("requested proxy service was not found"));

        return new ProxyServiceDestination(serviceDid, NormalizeServiceUrl(endpoint));
    }

    private static async Task<DidDocument> ResolveProxyDidDocumentAsync(string serviceDid, IdResolver idResolver)
    {
        try
        {
            return await idResolver.DidResolver.EnsureResolveAsync(serviceDid);
        }
        catch (Exception e)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("could not resolve atproto-proxy service DID"), e);
        }
    }

    private static async Task<ProxyServiceDestination> ResolveNotificationTargetAsync(string serviceDid, IBskyAppViewConfig config, IdResolver idResolver)
    {
        if (config is BskyAppViewConfig appViewConfig &&
            string.Equals(serviceDid, appViewConfig.Did, StringComparison.Ordinal))
        {
            return new ProxyServiceDestination(serviceDid, NormalizeServiceUrl(appViewConfig.Url));
        }

        var didDoc = await ResolveProxyDidDocumentAsync(serviceDid, idResolver);
        var endpoint = DidDoc.GetServiceEndpoint(didDoc, "bsky_notif", null);
        if (endpoint == null)
            throw new XRPCError(new InvalidRequestErrorDetail($"invalid notification service details in did document: {serviceDid}"));

        return new ProxyServiceDestination(serviceDid, NormalizeServiceUrl(endpoint));
    }

    private static (string serviceDid, string serviceId) ParseAtprotoProxyHeader(string headerValue)
    {
        var value = headerValue.Trim();
        var separator = value.LastIndexOf('#');
        if (separator <= 0 || separator == value.Length - 1)
            throw new XRPCError(new InvalidRequestErrorDetail("invalid atproto-proxy header"));

        var serviceDid = value[..separator];
        var serviceId = value[(separator + 1)..];
        if (!serviceDid.StartsWith("did:", StringComparison.Ordinal) ||
            serviceId.Any(ch => char.IsWhiteSpace(ch) || ch is '#' or '/' or '?'))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("invalid atproto-proxy header"));
        }

        return (serviceDid, serviceId);
    }

    private static string NormalizeServiceUrl(string url) => url.TrimEnd('/');

    private static string ParseUrlNsid(string requestUrl)
    {
        if (!requestUrl.StartsWith("/xrpc/"))
            throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));

        var nsid = requestUrl[6..];
        var alphaNumRequired = true;
        var curr = 0;
        for (; curr < nsid.Length; curr++)
        {
            var currentChar = nsid[curr];
            if (currentChar is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                alphaNumRequired = false;
            }
            else if (currentChar is '-' or '.')
            {
                if (alphaNumRequired) throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
                alphaNumRequired = true;
            }
            else if (currentChar is '/')
            {
                if (curr == nsid.Length - 1 || nsid[curr + 1] == '?') break;
                throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
            }
            else if (currentChar == '?') break;
            else throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
        }

        if (alphaNumRequired) throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));
        if (curr < 2) throw new XRPCError(new InvalidRequestErrorDetail("invalid xrpc path"));

        return nsid[..curr];
    }

    private readonly record struct ProxyServiceDestination(string Did, string Url);

    public sealed record RegisterPushRequest(
        [property: JsonPropertyName("serviceDid")] string ServiceDid,
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("platform")] string Platform,
        [property: JsonPropertyName("appId")] string AppId,
        [property: JsonPropertyName("ageRestricted")] bool? AgeRestricted);
}
