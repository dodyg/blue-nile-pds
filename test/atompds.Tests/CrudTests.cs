using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class CrudTests
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

    private async Task<(AccountInfo account, string uri, string cid)> CreatePostAsync(AccountInfo? account = null)
    {
        account ??= await CreateAccountAsync();
        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = $"Hello world {Guid.NewGuid():N}"[..30],
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        return (account, json.GetProperty("uri").GetString()!, json.GetProperty("cid").GetString()!);
    }

    [Test]
    public async Task CreateRecord_Succeeds()
    {
        var (account, uri, cid) = await CreatePostAsync();

        await Assert.That(uri).IsNotNull();
        await Assert.That(cid).IsNotNull();
    }

    [Test]
    public async Task CreateRecord_ReturnsValidAtUri()
    {
        var (account, uri, _) = await CreatePostAsync();

        await Assert.That(uri).StartsWith($"at://{account.Did}/app.bsky.feed.post/");
    }

    [Test]
    public async Task GetRecord_ReturnsCreated()
    {
        var (account, uri, _) = await CreatePostAsync();
        var rkey = uri.Split('/').Last();

        var response = await Client.GetAsync(
            $"/xrpc/com.atproto.repo.getRecord?repo={account.Did}&collection=app.bsky.feed.post&rkey={rkey}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("uri").GetString()).IsEqualTo(uri);
        await Assert.That(json.TryGetProperty("value", out _)).IsTrue();
    }

    [Test]
    public async Task ListRecords_ReturnsCreated()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);
        await CreatePostAsync(account);

        var response = await Client.GetAsync(
            $"/xrpc/com.atproto.repo.listRecords?repo={account.Did}&collection=app.bsky.feed.post");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var records = json.GetProperty("records").EnumerateArray().ToList();
        await Assert.That(records.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task ListRecords_Pagination()
    {
        var account = await CreateAccountAsync();
        for (int i = 0; i < 5; i++)
        {
            await CreatePostAsync(account);
        }

        var response = await Client.GetAsync(
            $"/xrpc/com.atproto.repo.listRecords?repo={account.Did}&collection=app.bsky.feed.post&limit=2");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var records = json.GetProperty("records").EnumerateArray().ToList();
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(json.TryGetProperty("cursor", out _)).IsTrue();
    }

    [Test]
    public async Task PutRecord_Succeeds()
    {
        var (account, uri, _) = await CreatePostAsync();
        var rkey = uri.Split('/').Last();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["rkey"] = rkey,
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = "Updated text",
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.putRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task PutRecord_CreateIfMissing()
    {
        var account = await CreateAccountAsync();
        var rkey = $"new-{Guid.NewGuid():N}"[..8];

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["rkey"] = rkey,
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = "Created via put",
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.putRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeleteRecord_Succeeds()
    {
        var (account, uri, _) = await CreatePostAsync();
        var rkey = uri.Split('/').Last();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["rkey"] = rkey
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.deleteRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DeleteRecord_NonExistent_NoError()
    {
        var account = await CreateAccountAsync();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["rkey"] = "nonexistent-rkey"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.deleteRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ApplyWrites_Create()
    {
        var account = await CreateAccountAsync();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["writes"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["$type"] = "com.atproto.repo.applyWrites#create",
                    ["collection"] = "app.bsky.feed.post",
                    ["rkey"] = $"aw{Guid.NewGuid():N}"[..8],
                    ["value"] = new Dictionary<string, object?>
                    {
                        ["$type"] = "app.bsky.feed.post",
                        ["text"] = "Created via applyWrites",
                        ["createdAt"] = DateTime.UtcNow.ToString("o")
                    }
                }
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.applyWrites")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ApplyWrites_Update()
    {
        var (account, uri, _) = await CreatePostAsync();
        var rkey = uri.Split('/').Last();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["writes"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["$type"] = "com.atproto.repo.applyWrites#update",
                    ["collection"] = "app.bsky.feed.post",
                    ["rkey"] = rkey,
                    ["value"] = new Dictionary<string, object?>
                    {
                        ["$type"] = "app.bsky.feed.post",
                        ["text"] = "Updated via applyWrites",
                        ["createdAt"] = DateTime.UtcNow.ToString("o")
                    }
                }
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.applyWrites")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ApplyWrites_Delete()
    {
        var (account, uri, _) = await CreatePostAsync();
        var rkey = uri.Split('/').Last();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["writes"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["$type"] = "com.atproto.repo.applyWrites#delete",
                    ["collection"] = "app.bsky.feed.post",
                    ["rkey"] = rkey
                }
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.applyWrites")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ApplyWrites_BatchOperations()
    {
        var account = await CreateAccountAsync();

        var writes = Enumerable.Range(0, 3).Select(_ => new Dictionary<string, object?>
        {
            ["$type"] = "com.atproto.repo.applyWrites#create",
            ["collection"] = "app.bsky.feed.post",
            ["rkey"] = $"b{Guid.NewGuid():N}"[..8],
            ["value"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = "Batch created",
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        }).ToArray();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["writes"] = writes
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.applyWrites")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task CreateRecord_WrongUser_ReturnsError()
    {
        var account1 = await CreateAccountAsync();
        var account2 = await CreateAccountAsync();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account2.Did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = "Unauthorized",
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account1.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateRecord_InvalidCollection_ReturnsError()
    {
        var account = await CreateAccountAsync();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "invalid collection!",
            ["record"] = new Dictionary<string, object?> { ["text"] = "test" }
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
    public async Task DescribeRepo_ReturnsInfo()
    {
        var account = await CreateAccountAsync();

        var response = await Client.GetAsync(
            $"/xrpc/com.atproto.repo.describeRepo?repo={account.Did}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("handle").GetString()).IsNotNull();
        await Assert.That(json.GetProperty("did").GetString()).IsNotNull();
        await Assert.That(json.TryGetProperty("collections", out _)).IsTrue();
    }

    [Test]
    public async Task CreateRecord_ProfileSelfRkey()
    {
        var account = await CreateAccountAsync();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.actor.profile",
            ["rkey"] = "self",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.actor.profile",
                ["displayName"] = "Test User",
                ["description"] = "A test profile"
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var uri = json.GetProperty("uri").GetString()!;
        await Assert.That(uri).Contains("self");
    }
}
