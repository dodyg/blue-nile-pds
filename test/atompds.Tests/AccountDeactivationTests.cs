using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class AccountDeactivationTests
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
    public async Task DeactivateAccount_SetsDeactivatedAt()
    {
        var account = await CreateAccountAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deactivateAccount");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeactivateAccount_CheckAccountStatus_ShowsDeactivated()
    {
        var account = await CreateAccountAsync();

        var deactivateRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deactivateAccount");
        deactivateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        deactivateRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        await Client.SendAsync(deactivateRequest);

        var statusRequest = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.server.checkAccountStatus");
        statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var statusResponse = await Client.SendAsync(statusRequest);

        await Assert.That(statusResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ActivateAccount_ClearsDeactivatedAt()
    {
        var account = await CreateAccountAsync();

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
    public async Task DeactivatedAccount_CannotCreateRecords()
    {
        var account = await CreateAccountAsync();

        var deactivateRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deactivateAccount");
        deactivateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        deactivateRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        await Client.SendAsync(deactivateRequest);

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = "Should fail",
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeleteAccount_RemovesFromListRepos()
    {
        var account = await CreateAccountAsync();

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

        var reposResponse = await Client.GetAsync("/xrpc/com.atproto.sync.listRepos?limit=1000");
        var reposJson = await AuthTestHelper.ReadJsonAsync(reposResponse);
        var repos = reposJson.GetProperty("repos").EnumerateArray().ToList();
        await Assert.That(repos.All(r => r.GetProperty("did").GetString() != account.Did)).IsTrue();
    }
}
