using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class EmailTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    [Test]
    public async Task ConfirmEmail_WithToken_Succeeds()
    {
        var account = await CreateAccountAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var token = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.EmailTokens.Where(t =>
                t.Did == account.Did &&
                t.Purpose == AccountManager.Db.EmailToken.EmailTokenPurpose.confirm_email));

        if (token != null)
        {
            var body = new Dictionary<string, object?> { ["token"] = token.Token };
            var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.confirmEmail")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
            var response = await Client.SendAsync(request);

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task ConfirmEmail_InvalidToken_ReturnsError()
    {
        var account = await CreateAccountAsync();

        var body = new Dictionary<string, object?> { ["token"] = "invalid-token" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.confirmEmail")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task RequestEmailConfirmation_Succeeds()
    {
        var account = await CreateAccountAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestEmailConfirmation");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task RequestEmailUpdate_Succeeds()
    {
        var account = await CreateAccountAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestEmailUpdate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task UpdateEmail_WithToken_Succeeds()
    {
        var account = await CreateAccountAsync();

        var updateRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestEmailUpdate");
        updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        await Client.SendAsync(updateRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var token = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.EmailTokens.Where(t =>
                t.Did == account.Did &&
                t.Purpose == AccountManager.Db.EmailToken.EmailTokenPurpose.update_email));

        if (token != null)
        {
            var newEmail = UniqueEmail();
            var body = new Dictionary<string, object?> { ["email"] = newEmail, ["token"] = token.Token };
            var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.updateEmail")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
            var response = await Client.SendAsync(request);

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task UpdateEmail_InvalidToken_ReturnsError()
    {
        var account = await CreateAccountAsync();

        var confirmBody = new Dictionary<string, object?> { ["token"] = "invalid-token" };
        var confirmRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.confirmEmail")
        {
            Content = new StringContent(JsonSerializer.Serialize(confirmBody), System.Text.Encoding.UTF8, "application/json")
        };
        confirmRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var confirmResponse = await Client.SendAsync(confirmRequest);

        await Assert.That(confirmResponse.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }
}
