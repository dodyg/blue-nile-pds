using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BlueNilePds.Host.Tests.Infrastructure;

namespace BlueNilePds.Host.Tests;

public class UserDataExportTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

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

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    private async Task CreatePostAsync(AccountInfo account, string text)
    {
        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = text,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> UploadBlobAndAttachAsync(AccountInfo account)
    {
        var png = CreateTestPng();
        var upload = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.uploadBlob")
        {
            Content = new ByteArrayContent(png)
        };
        upload.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Content.Headers.ContentLength = png.Length;
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var uploadResponse = await Client.SendAsync(upload);
        uploadResponse.EnsureSuccessStatusCode();

        var json = await AuthTestHelper.ReadJsonAsync(uploadResponse);
        var cid = json.GetProperty("blob").GetProperty("ref").GetProperty("$link").GetString()!;

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
                                ["size"] = png.Length
                            }
                        }
                    }
                }
            }
        };
        var create = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var createResponse = await Client.SendAsync(create);
        createResponse.EnsureSuccessStatusCode();

        return cid;
    }

    private HttpRequestMessage CreateExportRequest(string did, bool withAuth)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/export/user?did={Uri.EscapeDataString(did)}");
        if (withAuth)
            request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        return request;
    }

    [Test]
    public async Task ExportUser_NoAuth_ReturnsUnauthorized()
    {
        var response = await Client.SendAsync(CreateExportRequest("did:plc:test", withAuth: false));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ExportUser_UnknownDid_ReturnsError()
    {
        var response = await Client.SendAsync(CreateExportRequest("did:plc:doesnotexist", withAuth: true));
        await Assert.That((int)response.StatusCode).IsGreaterThanOrEqualTo(400);
    }

    [Test]
    public async Task ExportUser_ReturnsZipWithRecordsAndBlobs()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account, "Hello export");
        var blobCid = await UploadBlobAndAttachAsync(account);
        var png = CreateTestPng();

        var response = await Client.SendAsync(CreateExportRequest(account.Did, withAuth: true));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/zip");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var names = archive.Entries.Select(e => e.FullName).ToList();
        await Assert.That(names).Contains("manifest.json");
        await Assert.That(names).Contains("blobs/manifest.json");
        await Assert.That(names.Any(n => n.StartsWith("records/app.bsky.feed.post/") && n.EndsWith(".json"))).IsTrue();
        await Assert.That(names).Contains($"blobs/{blobCid}.png");

        var manifestEntry = archive.GetEntry("manifest.json")!;
        await using var manifestStream = manifestEntry.Open();
        using var manifestDoc = await JsonDocument.ParseAsync(manifestStream);
        await Assert.That(manifestDoc.RootElement.GetProperty("did").GetString()).IsEqualTo(account.Did);
        await Assert.That(manifestDoc.RootElement.GetProperty("recordCount").GetInt32()).IsGreaterThanOrEqualTo(2);
        await Assert.That(manifestDoc.RootElement.GetProperty("blobCount").GetInt32()).IsEqualTo(1);

        var blobEntry = archive.GetEntry($"blobs/{blobCid}.png")!;
        await using var blobStream = blobEntry.Open();
        using var ms = new MemoryStream();
        await blobStream.CopyToAsync(ms);
        await Assert.That(ms.ToArray()).IsEquivalentTo(png);

        var recordEntry = archive.Entries.First(e => e.FullName.StartsWith("records/app.bsky.feed.post/"));
        await using var recordStream = recordEntry.Open();
        using var recordDoc = await JsonDocument.ParseAsync(recordStream);
        await Assert.That(recordDoc.RootElement.TryGetProperty("value", out _)).IsTrue();
    }
}
