using System.Net;
using atompds.Tests.Infrastructure;

namespace atompds.Tests;

public class ModerationTempTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task CreateReport_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.moderation.createReport", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateReport_WithValidToken_NullBody_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.moderation.createReport");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CheckSignupQueue_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.temp.checkSignupQueue");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CheckSignupQueue_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken(scope: "com.atproto.signupQueued");
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.temp.checkSignupQueue");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }
}
