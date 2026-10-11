using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using BlueNilePds.Host.Tests.Infrastructure;

namespace BlueNilePds.Host.Tests;

public class RootEndpointsTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private HttpClient NoRedirectClient => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    private static HttpRequestMessage BrowserRootRequest(string host)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = host;
        request.Headers.Accept.ParseAdd("text/html");
        return request;
    }

    [Test]
    public async Task Root_ReturnsOk()
    {
        var response = await Client.GetAsync("/");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Root_ContainsServiceName()
    {
        var response = await Client.GetAsync("/");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("serviceName", out _)).IsTrue();
    }

    [Test]
    public async Task Root_ContainsDid()
    {
        var response = await Client.GetAsync("/");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("did", out _)).IsTrue();
    }

    [Test]
    public async Task Root_ContainsVersion()
    {
        var response = await Client.GetAsync("/");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("version", out _)).IsTrue();
    }

    [Test]
    public async Task Root_ContainsAvailableUserDomains()
    {
        var response = await Client.GetAsync("/");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("availableUserDomains", out _)).IsTrue();
    }

    [Test]
    public async Task Root_ContainsContactEmail()
    {
        var response = await Client.GetAsync("/");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("contactEmail", out _)).IsTrue();
    }

    [Test]
    public async Task Root_ContainsLinks()
    {
        var response = await Client.GetAsync("/");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("links", out _)).IsTrue();
    }

    [Test]
    public async Task Root_LinksContainHome()
    {
        var response = await Client.GetAsync("/");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var links = json.GetProperty("links");
        await Assert.That(links.TryGetProperty("home", out _)).IsTrue();
    }

    [Test]
    public async Task Root_LinksContainSupport()
    {
        var response = await Client.GetAsync("/");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var links = json.GetProperty("links");
        await Assert.That(links.TryGetProperty("support", out _)).IsTrue();
    }

    [Test]
    public async Task Root_BrowserHandleSubdomain_RedirectsToBskyProfile()
    {
        // Test env PDS_HOSTNAME is localhost, so alice.localhost is a handle subdomain.
        var response = await NoRedirectClient.SendAsync(BrowserRootRequest("alice.localhost"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Found);
        await Assert.That(response.Headers.Location?.ToString()).IsEqualTo("https://bsky.app/profile/alice.localhost");
    }

    [Test]
    public async Task Root_BrowserApexDomain_DoesNotRedirect()
    {
        var response = await NoRedirectClient.SendAsync(BrowserRootRequest("localhost"));
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Found);
    }

    [Test]
    public async Task Root_BrowserReservedSubdomain_DoesNotRedirect()
    {
        var response = await NoRedirectClient.SendAsync(BrowserRootRequest("www.localhost"));
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Found);
    }

    [Test]
    public async Task Root_ApiClientHandleSubdomain_ReturnsJson()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "alice.localhost";
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("serviceName", out _)).IsTrue();
    }

    [Test]
    public async Task RobotsTxt_ReturnsOk()
    {
        var response = await Client.GetAsync("/robots.txt");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task RobotsTxt_ContainsUserAgent()
    {
        var response = await Client.GetAsync("/robots.txt");
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content.Contains("User-agent:")).IsTrue();
    }

    [Test]
    public async Task RobotsTxt_AllowsXrpc()
    {
        var response = await Client.GetAsync("/robots.txt");
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content.Contains("Allow: /xrpc/")).IsTrue();
    }

    [Test]
    public async Task TlsCheck_ReturnsOk()
    {
        var response = await Client.GetAsync("/tls-check");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task TlsCheck_ContainsProto()
    {
        var response = await Client.GetAsync("/tls-check");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("proto", out _)).IsTrue();
    }

    [Test]
    public async Task TlsCheck_ContainsHost()
    {
        var response = await Client.GetAsync("/tls-check");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("host", out _)).IsTrue();
    }
}
