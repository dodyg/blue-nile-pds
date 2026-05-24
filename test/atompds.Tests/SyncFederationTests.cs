using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class SyncFederationTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    private async Task CreatePostAsync(AccountInfo account)
    {
        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = $"Sync test {Guid.NewGuid():N}"[..20],
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task GetRepo_ReturnsCarFile()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);

        var response = await Client.GetAsync($"/xrpc/com.atproto.sync.getRepo?did={account.Did}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType!.ToString()).Contains("car");
    }

    [Test]
    public async Task GetRepo_NonExistentDid_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getRepo?did=did:plc:nonexistentnonexistentnonexistent");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetLatestCommit_ReturnsCid()
    {
        var account = await CreateAccountAsync();

        var response = await Client.GetAsync($"/xrpc/com.atproto.sync.getLatestCommit?did={account.Did}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("cid", out _)).IsTrue();
        await Assert.That(json.TryGetProperty("rev", out _)).IsTrue();
    }

    [Test]
    public async Task GetLatestCommit_NonExistentDid_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getLatestCommit?did=did:plc:nonexistentnonexistentnonexistent");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetRepoStatus_Active_ReturnsActive()
    {
        var account = await CreateAccountAsync();

        var response = await Client.GetAsync($"/xrpc/com.atproto.sync.getRepoStatus?did={account.Did}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("active").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task GetRepoStatus_NonExistent_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getRepoStatus?did=did:plc:nonexistentnonexistentnonexistent");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task SyncGetRecord_ReturnsRecord()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);

        var listResponse = await Client.GetAsync(
            $"/xrpc/com.atproto.repo.listRecords?repo={account.Did}&collection=app.bsky.feed.post&limit=1");
        var listJson = await AuthTestHelper.ReadJsonAsync(listResponse);
        var records = listJson.GetProperty("records").EnumerateArray().ToList();
        await Assert.That(records.Count).IsGreaterThan(0);
        var rkey = records[0].GetProperty("uri").GetString()!.Split('/').Last();

        var response = await Client.GetAsync(
            $"/xrpc/com.atproto.sync.getRecord?did={account.Did}&collection=app.bsky.feed.post&rkey={rkey}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListRepos_ReturnsCreatedAccount()
    {
        var account = await CreateAccountAsync();

        var response = await Client.GetAsync("/xrpc/com.atproto.sync.listRepos?limit=100");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var repos = json.GetProperty("repos").EnumerateArray().ToList();
        await Assert.That(repos.Any(r => r.GetProperty("did").GetString() == account.Did)).IsTrue();
    }

    [Test]
    public async Task ListRepos_Pagination()
    {
        await CreateAccountAsync();
        await CreateAccountAsync();
        await CreateAccountAsync();

        var response = await Client.GetAsync("/xrpc/com.atproto.sync.listRepos?limit=2");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var repos = json.GetProperty("repos").EnumerateArray().ToList();
        await Assert.That(repos.Count).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task GetBlocks_ReturnsBlocks()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);

        var commitResponse = await Client.GetAsync($"/xrpc/com.atproto.sync.getLatestCommit?did={account.Did}");
        var commitJson = await AuthTestHelper.ReadJsonAsync(commitResponse);
        var cid = commitJson.GetProperty("cid").GetString();

        var response = await Client.GetAsync($"/xrpc/com.atproto.sync.getBlocks?did={account.Did}&cids={cid}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
