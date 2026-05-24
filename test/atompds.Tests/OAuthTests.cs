using System.Net;
using atompds.Tests.Infrastructure;

namespace atompds.Tests;

public class OAuthTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task Authorize_RouteExists()
    {
        var response = await Client.GetAsync("/oauth/authorize");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Authorize_WithParams_ReturnsNonNotFound()
    {
        var response = await Client.GetAsync("/oauth/authorize?client_id=test&redirect_uri=https://test.test/callback&scope=atproto&state=test&code_challenge=test&code_challenge_method=S256");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Consent_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/oauth/authorize/consent", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Token_NullBody_ReturnsError()
    {
        var response = await Client.PostAsync("/oauth/token", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ClientMetadata_RouteExists()
    {
        var response = await Client.GetAsync("/oauth/client-metadata.json");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ClientMetadata_WithClientId_ReturnsNonNotFound()
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/oauth/client-metadata.json?client_id=https://test.test/client-metadata.json");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
    }
}
