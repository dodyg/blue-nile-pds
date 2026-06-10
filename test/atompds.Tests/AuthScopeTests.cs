using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class AuthScopeTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    private HttpRequestMessage CreateRecordRequest(string token, string did)
    {
        var body = new Dictionary<string, object?>
        {
            ["repo"] = did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = "scope test",
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Test]
    public async Task AccessStandard_AcceptsAccessToken()
    {
        var account = await CreateAccountAsync();
        var request = CreateRecordRequest(account.AccessJwt, account.Did);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AccessStandard_AcceptsAppPassToken()
    {
        var account = await CreateAccountAsync();
        var token = AuthTestHelper.CreateAppPasswordToken(did: account.Did);
        var request = CreateRecordRequest(token, account.Did);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AccessStandard_AcceptsPrivilegedToken()
    {
        var account = await CreateAccountAsync();
        var token = AuthTestHelper.CreatePrivilegedToken(did: account.Did);
        var request = CreateRecordRequest(token, account.Did);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AccessStandard_RejectsRefreshToken()
    {
        var token = AuthTestHelper.CreateRefreshToken();
        var request = CreateRecordRequest(token, "did:plc:test");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AccessFull_RejectsAppPassToken()
    {
        var token = AuthTestHelper.CreateAppPasswordToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestAccountDelete");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AccessFull_AcceptsAccessToken()
    {
        var account = await CreateAccountAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestAccountDelete");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminToken_RejectsBearerToken()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.admin.getInviteCodes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminToken_AcceptsBasicAuth()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.admin.getInviteCodes");
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Refresh_OnlyAcceptsRefreshToken()
    {
        var account = await CreateAccountAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.refreshSession");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.RefreshJwt);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Refresh_RejectsAccessToken()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.refreshSession");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ExpiredToken_ReturnsError()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["scope"] = "com.atproto.access",
            ["sub"] = "did:plc:testtesttesttesttesttesttesttest",
            ["aud"] = TestWebAppFactory.ServiceDid,
            ["iat"] = now.AddHours(-3).ToUnixTimeSeconds(),
            ["exp"] = now.AddHours(-1).ToUnixTimeSeconds()
        };
        var headers = new Dictionary<string, object> { ["typ"] = "at+jwt" };
        var token = Jose.JWT.Encode(payload, System.Text.Encoding.UTF8.GetBytes(TestWebAppFactory.JwtSecret), Jose.JwsAlgorithm.HS256, headers);

        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.server.getSession");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }
}
