using System.Net;
using atompds.Tests.Infrastructure;

namespace atompds.Tests;

public class IdentityTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task ResolveHandle_MissingHandle_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.identity.resolveHandle");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ResolveHandle_WithHandle_ReturnsNonNotFound()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.identity.resolveHandle?handle=test.test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateHandle_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.identity.updateHandle", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateHandle_WithValidToken_NullBody_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.updateHandle");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SubmitPlcOperation_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.identity.submitPlcOperation", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SubmitPlcOperation_WithValidToken_NullBody_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.submitPlcOperation");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SignPlcOperation_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.identity.signPlcOperation", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SignPlcOperation_WithValidToken_NullBody_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.signPlcOperation");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RequestPlcOperationSignature_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.identity.requestPlcOperationSignature", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RequestPlcOperationSignature_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.requestPlcOperationSignature");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetRecommendedDidCredentials_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.identity.getRecommendedDidCredentials");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetRecommendedDidCredentials_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.identity.getRecommendedDidCredentials");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }
}
