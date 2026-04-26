using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class AppPasswordTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();
    private static int _counter;

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    [Test]
    public async Task CreateAppPassword_Succeeds()
    {
        var account = await CreateAccountAsync();

        var body = new Dictionary<string, object?> { ["name"] = "test-app" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAppPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("name").GetString()).IsEqualTo("test-app");
        await Assert.That(json.TryGetProperty("password", out _)).IsTrue();
    }

    [Test]
    public async Task ListAppPasswords_ReturnsCreated()
    {
        var account = await CreateAccountAsync();

        var createBody = new Dictionary<string, object?> { ["name"] = "my-app" };
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAppPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(createBody), Encoding.UTF8, "application/json")
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        await Client.SendAsync(createRequest);

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.server.listAppPasswords");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var listResponse = await Client.SendAsync(listRequest);

        await Assert.That(listResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(listResponse);
        var passwords = json.GetProperty("passwords").EnumerateArray().ToList();
        await Assert.That(passwords.Count).IsGreaterThan(0);
        await Assert.That(passwords.Any(p => p.GetProperty("name").GetString() == "my-app")).IsTrue();
    }

    [Test]
    public async Task RevokeAppPassword_Succeeds()
    {
        var account = await CreateAccountAsync();

        var createBody = new Dictionary<string, object?> { ["name"] = "revoke-me" };
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAppPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(createBody), Encoding.UTF8, "application/json")
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        await Client.SendAsync(createRequest);

        var revokeBody = new Dictionary<string, object?> { ["name"] = "revoke-me" };
        var revokeRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.revokeAppPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(revokeBody), Encoding.UTF8, "application/json")
        };
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var revokeResponse = await Client.SendAsync(revokeRequest);

        await Assert.That(revokeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.server.listAppPasswords");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var listResponse = await Client.SendAsync(listRequest);
        var json = await AuthTestHelper.ReadJsonAsync(listResponse);
        var passwords = json.GetProperty("passwords").EnumerateArray().ToList();
        await Assert.That(passwords.All(p => p.GetProperty("name").GetString() != "revoke-me")).IsTrue();
    }

    [Test]
    public async Task CreateAppPassword_DuplicateName_ReturnsError()
    {
        var account = await CreateAccountAsync();

        var body = new Dictionary<string, object?> { ["name"] = "dup-name" };
        for (int i = 0; i < 2; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAppPassword")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
            var response = await Client.SendAsync(request);

            if (i == 1)
            {
                await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            }
        }
    }

    [Test]
    public async Task AppPasswordToken_GrantsAccess()
    {
        var email = UniqueEmail();
        var handle = UniqueHandle();
        var account = await AccountHelper.CreateAccountAsync(Client, handle: handle, email: email);

        var createBody = new Dictionary<string, object?> { ["name"] = "login-app" };
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAppPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(createBody), Encoding.UTF8, "application/json")
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var createResponse = await Client.SendAsync(createRequest);
        var createJson = await AuthTestHelper.ReadJsonAsync(createResponse);
        var appPassword = createJson.GetProperty("password").GetString();

        var loginBody = new Dictionary<string, object?>
        {
            ["identifier"] = email,
            ["password"] = appPassword
        };
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createSession")
        {
            Content = new StringContent(JsonSerializer.Serialize(loginBody), Encoding.UTF8, "application/json")
        };
        var loginResponse = await Client.SendAsync(loginRequest);

        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
