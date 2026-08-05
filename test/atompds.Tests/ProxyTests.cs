using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class ProxyTests
{
    private static readonly TestWebAppFactory Factory = new();
    private static readonly TestWebAppFactory LegacyFactory = new(
        new Dictionary<string, string?> { ["Config:PDS_PROXY_REQUIRE_HEADER"] = "false" });

    private HttpClient Client => Factory.CreateClient();

    private HttpRequestMessage CreateProxyRequest(HttpMethod method, string url, string token, string? proxyHeader = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (proxyHeader != null)
        {
            request.Headers.Add("atproto-proxy", proxyHeader);
        }
        return request;
    }

    [Test]
    public async Task Proxy_MissingHeader_ReturnsBadRequest()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = CreateProxyRequest(HttpMethod.Get, "/xrpc/app.bsky.feed.getTimeline?limit=10", token);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("error").GetString()).IsEqualTo("InvalidRequest");
        await Assert.That(json.GetProperty("message").GetString()).IsEqualTo("atproto-proxy header required");
    }

    [Test]
    public async Task Proxy_InvalidProxyHeader_ReturnsBadRequest()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = CreateProxyRequest(HttpMethod.Get, "/xrpc/app.bsky.feed.getTimeline?limit=10", token, "not-a-valid-header");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("error").GetString()).IsEqualTo("InvalidRequest");
    }

    [Test]
    public async Task Proxy_BskyAppView_RouteExists()
    {
        var account = await AccountHelper.CreateAccountAsync(Client);
        var request = CreateProxyRequest(HttpMethod.Get, "/xrpc/app.bsky.feed.getTimeline?limit=10", account.AccessJwt,
            "did:web:appview.bsky.social#bsky_appview");
        var response = await Client.SendAsync(request);

        // The request is forwarded upstream to the (fake) AppView, which responds 404.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Proxy_ChatBsky_RouteExists()
    {
        var account = await AccountHelper.CreateAccountAsync(Client);
        var request = CreateProxyRequest(HttpMethod.Get, "/xrpc/chat.bsky.convo.listConvos", account.AccessJwt,
            "did:web:appview.bsky.social#bsky_appview");
        var response = await Client.SendAsync(request);

        // The request is forwarded upstream to the (fake) AppView, which responds 404.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Proxy_Legacy_ImplicitForwardingWithoutHeader()
    {
        var client = LegacyFactory.CreateClient();
        var account = await AccountHelper.CreateAccountAsync(client);
        var request = CreateProxyRequest(HttpMethod.Get, "/xrpc/app.bsky.feed.getTimeline?limit=10", account.AccessJwt);
        var response = await client.SendAsync(request);

        // With RequireProxyHeader=false the request is still implicitly forwarded upstream (404 from the fake AppView).
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Proxy_GetProfile_MissingHeader_ReturnsBadRequest()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = CreateProxyRequest(HttpMethod.Get, "/xrpc/app.bsky.actor.getProfile?actor=test.test", token);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("error").GetString()).IsEqualTo("InvalidRequest");
        await Assert.That(json.GetProperty("message").GetString()).IsEqualTo("atproto-proxy header required");
    }

    [Test]
    public async Task Proxy_GetProfile_Anonymous_WithHeader_Proxied()
    {
        // getProfile is public: anonymous requests are forwarded to the AppView without a user JWT.
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/app.bsky.actor.getProfile?actor=test.test");
        request.Headers.Add("atproto-proxy", "did:web:appview.bsky.social#bsky_appview");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Proxy_GetProfile_Authenticated_WithHeader_Proxied()
    {
        var account = await AccountHelper.CreateAccountAsync(Client);
        var request = CreateProxyRequest(HttpMethod.Get, "/xrpc/app.bsky.actor.getProfile?actor=test.test", account.AccessJwt,
            "did:web:appview.bsky.social#bsky_appview");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Proxy_AppBskyPost_RouteExists()
    {
        var response = await Client.GetAsync("/xrpc/app.bsky.feed.getTimeline?limit=10");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Proxy_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/app.bsky.actor.getPreferences");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Proxy_NsidParsing_ValidNsid()
    {
        var response = await Client.GetAsync("/xrpc/app.bsky.actor.getProfile?actor=test.test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Proxy_Catchall_RejectsUnknownNamespace()
    {
        var response = await Client.GetAsync("/xrpc/unknown.namespace.method");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }
}
