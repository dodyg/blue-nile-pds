using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class AccountDeletionTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    private async Task<(AccountInfo account, string token)> RequestDeleteTokenAsync()
    {
        var account = await CreateAccountAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestAccountDelete");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        await Client.SendAsync(request);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var token = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.EmailTokens.Where(t =>
                t.Did == account.Did &&
                t.Purpose == AccountManager.Db.EmailToken.EmailTokenPurpose.delete_account));

        return (account, token!.Token);
    }

    [Test]
    public async Task RequestAccountDelete_CreatesToken()
    {
        var account = await CreateAccountAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.requestAccountDelete");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var token = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.EmailTokens.Where(t =>
                t.Did == account.Did &&
                t.Purpose == AccountManager.Db.EmailToken.EmailTokenPurpose.delete_account));
        await Assert.That(token).IsNotNull();
    }

    [Test]
    public async Task DeleteAccount_WithValidToken_Succeeds()
    {
        var (account, token) = await RequestDeleteTokenAsync();

        var body = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["password"] = account.Password,
            ["token"] = token
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deleteAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeleteAccount_WithWrongToken_ReturnsError()
    {
        var (account, _) = await RequestDeleteTokenAsync();

        var body = new Dictionary<string, object?>
        {
            ["did"] = account.Did,
            ["password"] = account.Password,
            ["token"] = "wrong-token-value"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deleteAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeleteAccount_WithTokenFromOtherAccount_ReturnsError()
    {
        var (account1, token1) = await RequestDeleteTokenAsync();
        var account2 = await CreateAccountAsync();

        var body = new Dictionary<string, object?>
        {
            ["did"] = account2.Did,
            ["password"] = account2.Password,
            ["token"] = token1
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.deleteAccount")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }
}
