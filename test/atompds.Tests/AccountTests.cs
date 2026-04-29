using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class AccountTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();
    private static int _counter;

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    [Test]
    public async Task CreateAccount_BasicSucceeds()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        await Assert.That(account.Did).IsNotNull();
        await Assert.That(account.Handle).IsNotNull();
        await Assert.That(account.AccessJwt).IsNotNull();
        await Assert.That(account.RefreshJwt).IsNotNull();
    }

    [Test]
    public async Task CreateAccount_ReturnsDidPlc()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        await Assert.That(account.Did).StartsWith("did:plc:");
    }

    [Test]
    public async Task CreateAccount_DuplicateEmail_ReturnsError()
    {
        var email = UniqueEmail();
        await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: email);

        var body = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["handle"] = UniqueHandle(),
            ["password"] = "test-password-123"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateAccount_DuplicateHandle_ReturnsError()
    {
        var handle = UniqueHandle();
        await AccountHelper.CreateAccountAsync(Client, handle: handle, email: UniqueEmail());

        var body = new Dictionary<string, object?>
        {
            ["email"] = UniqueEmail(),
            ["handle"] = handle,
            ["password"] = "test-password-123"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateAccount_InvalidHandle_ReturnsError()
    {
        var body = new Dictionary<string, object?>
        {
            ["email"] = UniqueEmail(),
            ["handle"] = "a",
            ["password"] = "test-password-123"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateAccount_InvalidEmail_ReturnsError()
    {
        var body = new Dictionary<string, object?>
        {
            ["email"] = "not-an-email",
            ["handle"] = UniqueHandle(),
            ["password"] = "test-password-123"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateAccount_ReservedHandle_ReturnsError()
    {
        var body = new Dictionary<string, object?>
        {
            ["email"] = UniqueEmail(),
            ["handle"] = "admin.test",
            ["password"] = "test-password-123"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateSession_BasicLogin()
    {
        var email = UniqueEmail();
        var handle = UniqueHandle();
        var password = "test-password-123";
        var account = await AccountHelper.CreateAccountAsync(Client, email: email, handle: handle, password: password);

        var body = new Dictionary<string, object?>
        {
            ["identifier"] = email,
            ["password"] = password
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createSession")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("did").GetString()).IsEqualTo(account.Did);
        await Assert.That(json.TryGetProperty("accessJwt", out _)).IsTrue();
        await Assert.That(json.TryGetProperty("refreshJwt", out _)).IsTrue();
    }

    [Test]
    public async Task CreateSession_BadPassword_ReturnsError()
    {
        var email = UniqueEmail();
        await AccountHelper.CreateAccountAsync(Client, email: email, handle: UniqueHandle(), password: "correct-password");

        var body = new Dictionary<string, object?>
        {
            ["identifier"] = email,
            ["password"] = "wrong-password"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createSession")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateSession_UnknownEmail_ReturnsError()
    {
        var body = new Dictionary<string, object?>
        {
            ["identifier"] = "nonexistent@test.test",
            ["password"] = "some-password"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createSession")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSession_WithValidAccess_ReturnsSession()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.server.getSession");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("did").GetString()).IsEqualTo(account.Did);
        await Assert.That(json.GetProperty("handle").GetString()!).IsNotNull();
    }

    [Test]
    public async Task RefreshSession_WithRefreshToken()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.refreshSession");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.RefreshJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("accessJwt", out _)).IsTrue();
        await Assert.That(json.TryGetProperty("refreshJwt", out _)).IsTrue();
    }

    [Test]
    public async Task RefreshSession_WithAccessToken_ReturnsError()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.refreshSession");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteSession_RevokesRefreshToken()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var deleteRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deleteSession");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.RefreshJwt);
        var deleteResponse = await Client.SendAsync(deleteRequest);
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.refreshSession");
        refreshRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.RefreshJwt);
        var refreshResponse = await Client.SendAsync(refreshRequest);

        await Assert.That(refreshResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RequestAccountDelete_Succeeds()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestAccountDelete");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeleteAccount_WithToken_Succeeds()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var deleteRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestAccountDelete");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        await Client.SendAsync(deleteRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var token = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.EmailTokens.Where(t =>
                t.Did == account.Did &&
                t.Purpose == AccountManager.Db.EmailToken.EmailTokenPurpose.delete_account));

        var body = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["password"] = account.Password,
            ["token"] = token!.Token
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deleteAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ActivateAccount_Succeeds()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var deactivateRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deactivateAccount");
        deactivateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        deactivateRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        await Client.SendAsync(deactivateRequest);

        var activateRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.activateAccount");
        activateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var activateResponse = await Client.SendAsync(activateRequest);

        await Assert.That(activateResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeactivateAccount_Succeeds()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deactivateAccount");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
