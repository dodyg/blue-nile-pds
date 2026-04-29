using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class PasswordTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    [Test]
    public async Task RequestPasswordReset_Succeeds()
    {
        var email = UniqueEmail();
        await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: email);

        var body = new Dictionary<string, object?> { ["email"] = email };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestPasswordReset")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ResetPassword_WithToken_Succeeds()
    {
        var email = UniqueEmail();
        var handle = UniqueHandle();
        var account = await AccountHelper.CreateAccountAsync(Client, handle: handle, email: email, password: "old-password");

        var resetBody = new Dictionary<string, object?> { ["email"] = email };
        var resetRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestPasswordReset")
        {
            Content = new StringContent(JsonSerializer.Serialize(resetBody), Encoding.UTF8, "application/json")
        };
        await Client.SendAsync(resetRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var token = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.EmailTokens.Where(t =>
                t.Did == account.Did &&
                t.Purpose == AccountManager.Db.EmailToken.EmailTokenPurpose.reset_password));

        var body = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["token"] = token!.Token,
            ["password"] = "new-password-456"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.resetPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginBody = new Dictionary<string, object?>
        {
            ["identifier"] = email,
            ["password"] = "new-password-456"
        };
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createSession")
        {
            Content = new StringContent(JsonSerializer.Serialize(loginBody), Encoding.UTF8, "application/json")
        };
        var loginResponse = await Client.SendAsync(loginRequest);
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ResetPassword_OldPasswordFails()
    {
        var email = UniqueEmail();
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: email, password: "old-password");

        var resetBody = new Dictionary<string, object?> { ["email"] = email };
        var resetRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestPasswordReset")
        {
            Content = new StringContent(JsonSerializer.Serialize(resetBody), Encoding.UTF8, "application/json")
        };
        await Client.SendAsync(resetRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var token = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.EmailTokens.Where(t =>
                t.Did == account.Did &&
                t.Purpose == AccountManager.Db.EmailToken.EmailTokenPurpose.reset_password));

        var body = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["token"] = token!.Token,
            ["password"] = "brand-new-password"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.resetPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        await Client.SendAsync(request);

        var loginBody = new Dictionary<string, object?>
        {
            ["identifier"] = email,
            ["password"] = "old-password"
        };
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createSession")
        {
            Content = new StringContent(JsonSerializer.Serialize(loginBody), Encoding.UTF8, "application/json")
        };
        var loginResponse = await Client.SendAsync(loginRequest);
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ResetPassword_InvalidToken_ReturnsError()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var body = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["token"] = "invalid-token-value",
            ["password"] = "new-password"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.resetPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ResetPassword_TokenSingleUse()
    {
        var email = UniqueEmail();
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: email);

        var resetBody = new Dictionary<string, object?> { ["email"] = email };
        var resetRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestPasswordReset")
        {
            Content = new StringContent(JsonSerializer.Serialize(resetBody), Encoding.UTF8, "application/json")
        };
        await Client.SendAsync(resetRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var token = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.EmailTokens.Where(t =>
                t.Did == account.Did &&
                t.Purpose == AccountManager.Db.EmailToken.EmailTokenPurpose.reset_password));

        var body1 = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["token"] = token!.Token,
            ["password"] = "first-reset"
        };
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.resetPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(body1), Encoding.UTF8, "application/json")
        };
        var response1 = await Client.SendAsync(request1);
        await Assert.That(response1.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body2 = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["token"] = token.Token,
            ["password"] = "second-reset"
        };
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.resetPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(body2), Encoding.UTF8, "application/json")
        };
        var response2 = await Client.SendAsync(request2);
        await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task AdminUpdatePassword_Succeeds()
    {
        var email = UniqueEmail();
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: email, password: "original-pw");

        var body = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["password"] = "admin-set-password"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.admin.updateAccountPassword")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginBody = new Dictionary<string, object?>
        {
            ["identifier"] = email,
            ["password"] = "admin-set-password"
        };
        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createSession")
        {
            Content = new StringContent(JsonSerializer.Serialize(loginBody), Encoding.UTF8, "application/json")
        };
        var loginResponse = await Client.SendAsync(loginRequest);
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
