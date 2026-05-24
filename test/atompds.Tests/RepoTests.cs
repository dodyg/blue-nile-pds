using System.Net;
using atompds.Tests.Infrastructure;

namespace atompds.Tests;

public class RepoTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task GetRecord_RouteExists()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.repo.getRecord?repo=did:plc:test&collection=app.bsky.feed.post&rkey=test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRecord_MissingRepo_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.repo.getRecord?collection=app.bsky.feed.post&rkey=test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ListRecords_RouteExists()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.repo.listRecords?repo=did:plc:test&collection=app.bsky.feed.post");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ListRecords_MissingParams_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.repo.listRecords?repo=did:plc:test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DescribeRepo_RouteExists()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.repo.describeRepo?repo=did:plc:test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DescribeRepo_MissingRepo_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.repo.describeRepo");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateRecord_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.repo.createRecord", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PutRecord_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.repo.putRecord", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteRecord_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.repo.deleteRecord", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ApplyWrites_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.repo.applyWrites", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UploadBlob_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.repo.uploadBlob", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateRecord_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PutRecord_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.putRecord");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteRecord_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.deleteRecord");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ApplyWrites_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.applyWrites");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UploadBlob_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.uploadBlob");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }
}
