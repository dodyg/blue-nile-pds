using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BlueNilePds.Host.Tests.Infrastructure;
using BlueNilePds.Pds.ActorStore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PeterO.Cbor;

namespace BlueNilePds.Host.Tests;

public class BlobTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    private static byte[] CreateTestPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
        0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    private async Task<(AccountInfo account, string cid)> UploadBlobAsync(AccountInfo? account = null)
    {
        account ??= await CreateAccountAsync();
        var png = CreateTestPng();

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.uploadBlob")
        {
            Content = new ByteArrayContent(png)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        request.Content.Headers.ContentLength = png.Length;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await AuthTestHelper.ReadJsonAsync(response);
        var cid = json.GetProperty("blob").GetProperty("ref").GetProperty("$link").GetString()!;
        return (account, cid);
    }

    private async Task<(AccountInfo account, string cid, string uri)> UploadBlobAndAttachAsync()
    {
        var (account, cid) = await UploadBlobAsync();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = "Post with blob",
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["embed"] = new Dictionary<string, object?>
                {
                    ["$type"] = "app.bsky.embed.images",
                    ["images"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["alt"] = "test",
                            ["image"] = new Dictionary<string, object?>
                            {
                                ["$type"] = "blob",
                                ["ref"] = new Dictionary<string, object?> { ["$link"] = cid },
                                ["mimeType"] = "image/png",
                                ["size"] = CreateTestPng().Length
                            }
                        }
                    }
                }
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var json = await AuthTestHelper.ReadJsonAsync(response);
        return (account, cid, json.GetProperty("uri").GetString()!);
    }

    [Test]
    public async Task UploadBlob_Succeeds()
    {
        var (_, cid) = await UploadBlobAsync();
        await Assert.That(cid).IsNotNull();
    }

    [Test]
    public async Task UploadBlob_ExceedsLimit_ReturnsError()
    {
        var account = await CreateAccountAsync();
        var largeBlob = new byte[6 * 1024 * 1024];

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.uploadBlob")
        {
            Content = new ByteArrayContent(largeBlob)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentLength = largeBlob.Length;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetBlob_ReturnsUploaded()
    {
        var (account, cid, _) = await UploadBlobAndAttachAsync();

        var response = await Client.GetAsync(
            $"/xrpc/com.atproto.sync.getBlob?did={account.Did}&cid={cid}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        await Assert.That(bytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task ListBlobs_ReturnsUploaded()
    {
        var (account, _, _) = await UploadBlobAndAttachAsync();

        var response = await Client.GetAsync(
            $"/xrpc/com.atproto.sync.listBlobs?did={account.Did}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var cids = json.GetProperty("cids").EnumerateArray().ToList();
        await Assert.That(cids.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task UploadBlob_NoAuth_ReturnsError()
    {
        var png = CreateTestPng();

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.uploadBlob")
        {
            Content = new ByteArrayContent(png)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UploadBlob_WithImage_Succeeds()
    {
        var (_, cid) = await UploadBlobAsync();
        await Assert.That(cid).IsNotNull();
    }

    private static string GetActorDbPath(string did)
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ActorRepositoryProvider>();
        return provider.GetLocation(did).DbLocation;
    }

    private static async Task<string> GetRecordCidAsync(string dbPath, string uri)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Cid FROM record WHERE Uri = @uri";
        cmd.Parameters.AddWithValue("@uri", uri);
        return await cmd.ExecuteScalarAsync() as string
            ?? throw new InvalidOperationException($"Record index row not found for {uri}");
    }

    private static async Task<byte[]> ReadBlockAsync(string dbPath, string cid)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Content FROM repo_block WHERE Cid = @cid";
        cmd.Parameters.AddWithValue("@cid", cid);
        return await cmd.ExecuteScalarAsync() as byte[]
            ?? throw new InvalidOperationException($"Block {cid} not found");
    }

    [Test]
    [NotInParallel]
    public async Task BlobPost_StoresLinkRefAndServesJsonLink()
    {
        var (account, cid, uri) = await UploadBlobAndAttachAsync();
        var rkey = uri.Split('/').Last();

        // Stored record block must use a tag-42 link, not a {"$link": ...} map.
        var dbPath = GetActorDbPath(account.Did);
        var recordCid = await GetRecordCidAsync(dbPath, uri);
        var record = CBORObject.DecodeFromBytes(await ReadBlockAsync(dbPath, recordCid));
        var storedRef = record["embed"]["images"][0]["image"]["ref"];
        await Assert.That(storedRef.IsTagged).IsTrue();
        await Assert.That((long)storedRef.GetAllTags()[0]).IsEqualTo(42L);
        await Assert.That(storedRef.Untag().Type).IsEqualTo(CBORType.ByteString);

        // And the API still renders the atproto JSON form.
        var response = await Client.GetAsync(
            $"/xrpc/com.atproto.repo.getRecord?repo={account.Did}&collection=app.bsky.feed.post&rkey={rkey}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var servedLink = json.GetProperty("value").GetProperty("embed").GetProperty("images")[0]
            .GetProperty("image").GetProperty("ref").GetProperty("$link").GetString();
        await Assert.That(servedLink).IsEqualTo(cid);
    }
}
