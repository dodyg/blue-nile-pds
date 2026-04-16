using System.Net;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class OAuthFlowTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task Authorize_MissingClientId_ReturnsError()
    {
        var response = await Client.GetAsync("/oauth/authorize?redirect_uri=http://localhost/cb&code_challenge=test&code_challenge_method=S256&response_type=code");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Authorize_MissingRedirectUri_ReturnsError()
    {
        var response = await Client.GetAsync("/oauth/authorize?client_id=test-client&code_challenge=test&code_challenge_method=S256&response_type=code");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Authorize_MissingCodeChallenge_ReturnsError()
    {
        var response = await Client.GetAsync("/oauth/authorize?client_id=test-client&redirect_uri=http://localhost/cb&code_challenge_method=S256&response_type=code");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Authorize_UnsupportedChallengeMethod_ReturnsError()
    {
        var response = await Client.GetAsync("/oauth/authorize?client_id=test-client&redirect_uri=http://localhost/cb&code_challenge=test&code_challenge_method=plain&response_type=code");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_Endpoint_Exists()
    {
        var response = await Client.PostAsync("/oauth/token",
            new StringContent("grant_type=authorization_code&code=test&code_verifier=test",
                Encoding.UTF8, "application/x-www-form-urlencoded"));

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Token_InvalidCode_ReturnsError()
    {
        var response = await Client.PostAsync("/oauth/token",
            new StringContent("grant_type=authorization_code&code=invalid-code&code_verifier=test-verifier",
                Encoding.UTF8, "application/x-www-form-urlencoded"));

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_InvalidCodeVerifier_ReturnsError()
    {
        var response = await Client.PostAsync("/oauth/token",
            new StringContent("grant_type=authorization_code&code=invalid&code_verifier=invalid",
                Encoding.UTF8, "application/x-www-form-urlencoded"));

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_UnsupportedGrantType_ReturnsError()
    {
        var response = await Client.PostAsync("/oauth/token",
            new StringContent("grant_type=client_credentials",
                Encoding.UTF8, "application/x-www-form-urlencoded"));

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_RefreshTokenGrant_SucceedsFormat()
    {
        var response = await Client.PostAsync("/oauth/token",
            new StringContent("grant_type=refresh_token&refresh_token=invalid-token",
                Encoding.UTF8, "application/x-www-form-urlencoded"));

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Authorize_RouteExists()
    {
        var response = await Client.GetAsync("/oauth/authorize?client_id=test&redirect_uri=http://localhost&code_challenge=test&code_challenge_method=S256&response_type=code");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Token_TokenEndpoint_RejectsEmptyBody()
    {
        var response = await Client.PostAsync("/oauth/token",
            new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded"));

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }
}
