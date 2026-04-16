using System.Net;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class ProxyTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task Proxy_BskyAppView_RouteExists()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Get,
            "/xrpc/app.bsky.actor.getPreferences");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Proxy_Catchall_RejectsUnknownNamespace()
    {
        var response = await Client.GetAsync("/xrpc/unknown.namespace.method");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Proxy_ChatBsky_RouteExists()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Get,
            "/xrpc/chat.bsky.convo.listConvos");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
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
}
