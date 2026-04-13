using System.Net;
using System.Text.Json;
using atompds.Tests.Infrastructure;

namespace atompds.Tests;

public class HealthErrorWellKnownTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task Health_ReturnsOk()
    {
        var response = await Client.GetAsync("/xrpc/_health");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Health_ContainsVersion()
    {
        var response = await Client.GetAsync("/xrpc/_health");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("version", out _)).IsTrue();
    }

    [Test]
    public async Task Health_VersionStartsWithAtompds()
    {
        var response = await Client.GetAsync("/xrpc/_health");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var version = json.GetProperty("version").GetString();
        await Assert.That(version).IsNotNull();
        await Assert.That(version!.StartsWith("atompds")).IsTrue();
    }

    [Test]
    public async Task Error_ReturnsBadRequest()
    {
        var response = await Client.GetAsync("/error");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ErrorPost_ReturnsBadRequest()
    {
        var response = await Client.PostAsync("/error", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task WellKnown_OAuthProtectedResource_ReturnsOk()
    {
        var response = await Client.GetAsync(".well-known/oauth-protected-resource");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task WellKnown_OAuthProtectedResource_ContainsResource()
    {
        var response = await Client.GetAsync(".well-known/oauth-protected-resource");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("resource", out _)).IsTrue();
    }

    [Test]
    public async Task WellKnown_OAuthProtectedResource_ContainsAuthorizationServers()
    {
        var response = await Client.GetAsync(".well-known/oauth-protected-resource");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("authorization_servers", out _)).IsTrue();
    }

    [Test]
    public async Task WellKnown_OAuthAuthorizationServer_ReturnsOk()
    {
        var response = await Client.GetAsync(".well-known/oauth-authorization-server");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task WellKnown_OAuthAuthorizationServer_ContainsIssuer()
    {
        var response = await Client.GetAsync(".well-known/oauth-authorization-server");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("issuer", out _)).IsTrue();
    }

    [Test]
    public async Task WellKnown_OAuthAuthorizationServer_ContainsScopesSupported()
    {
        var response = await Client.GetAsync(".well-known/oauth-authorization-server");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("scopes_supported", out _)).IsTrue();
    }

    [Test]
    public async Task WellKnown_OAuthAuthorizationServer_ContainsAuthorizationEndpoint()
    {
        var response = await Client.GetAsync(".well-known/oauth-authorization-server");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("authorization_endpoint", out _)).IsTrue();
    }

    [Test]
    public async Task WellKnown_OAuthAuthorizationServer_ContainsTokenEndpoint()
    {
        var response = await Client.GetAsync(".well-known/oauth-authorization-server");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("token_endpoint", out _)).IsTrue();
    }

    [Test]
    public async Task WellKnown_AtprotoDid_ReturnsNotFoundForUnknownHandle()
    {
        var response = await Client.GetAsync(".well-known/atproto-did");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
