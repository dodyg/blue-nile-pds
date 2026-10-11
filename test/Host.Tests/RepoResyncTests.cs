using System.Formats.Cbor;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BlueNilePds.Pds.ActorStore;
using BlueNilePds.Host.Services;
using BlueNilePds.Host.Tests.Infrastructure;
using BlueNilePds.Core.Common;
using BlueNilePds.Core.Repo;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PeterO.Cbor;

namespace BlueNilePds.Host.Tests;

public class RepoResyncTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    private async Task<(AccountInfo account, string uri, string cid)> CreatePostAsync()
    {
        var account = await CreateAccountAsync();
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

    private static async Task<string> GetRecordIndexCidAsync(string dbPath, string uri)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Cid FROM record WHERE Uri = @uri";
        cmd.Parameters.AddWithValue("@uri", uri);
        var result = await cmd.ExecuteScalarAsync();
        return result as string ?? throw new InvalidOperationException($"Record index row not found for {uri}");
    }

    private static string GetActorDbPath(string did)
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ActorRepositoryProvider>();
        return provider.GetLocation(did).DbLocation;
    }

    /// <summary>
    /// Rewrite a record's stored block with non-canonical (PeterO default-order) bytes,
    /// reproducing the T-03 divergence: stored block bytes no longer hash to the record Cid.
    /// </summary>
    /// <summary>
    /// Rewrite a record's stored block with non-canonical bytes (same content, reversed map-key order),
    /// reproducing the T-03 divergence: stored block bytes no longer hash to the record Cid.
    /// </summary>
    private static async Task<(byte[] NonCanonicalBytes, byte[] CanonicalBytes)> InjectNonCanonicalBlockAsync(string dbPath, string recordCid)
    {
        var canonicalBytes = await ReadBlockBytesAsync(dbPath, recordCid)
            ?? throw new InvalidOperationException($"Block {recordCid} not found");
        var record = CBORObject.DecodeFromBytes(canonicalBytes);
        var nonCanonicalBytes = EncodeReversedKeys(record);
        // sanity: the injected bytes must actually be non-canonical for this test to be meaningful
        await Assert.That(nonCanonicalBytes.SequenceEqual(canonicalBytes)).IsFalse();
        await Assert.That(CBORObject.DecodeFromBytes(nonCanonicalBytes).ToJSONString()).IsEqualTo(record.ToJSONString());

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE repo_block SET Content = @content, Size = @size WHERE Cid = @cid";
        cmd.Parameters.AddWithValue("@content", nonCanonicalBytes);
        cmd.Parameters.AddWithValue("@size", nonCanonicalBytes.Length);
        cmd.Parameters.AddWithValue("@cid", recordCid);
        cmd.ExecuteNonQuery();

        return (nonCanonicalBytes, CanonicalDagCbor.Encode(record));
    }

    /// <summary>
    /// Encode a decoded record with map keys in reverse order so the bytes are
    /// valid CBOR but non-canonical (DAG-CBOR requires UTF-8-sorted map keys).
    /// </summary>
    private static byte[] EncodeReversedKeys(CBORObject obj)
    {
        var writer = new CborWriter(CborConformanceMode.Lax, false);
        WriteReversed(writer, obj);
        return writer.Encode();
    }

    private static void WriteReversed(CborWriter writer, CBORObject obj)
    {
        switch (obj.Type)
        {
            case CBORType.Map:
                var keys = obj.Keys.ToList();
                keys.Reverse();
                writer.WriteStartMap(keys.Count);
                foreach (var key in keys)
                {
                    writer.WriteTextString(key.AsString());
                    WriteReversed(writer, obj[key]);
                }
                writer.WriteEndMap();
                break;

            case CBORType.Array:
                writer.WriteStartArray(obj.Count);
                for (var i = 0; i < obj.Count; i++)
                {
                    WriteReversed(writer, obj[i]);
                }
                writer.WriteEndArray();
                break;

            case CBORType.TextString:
                writer.WriteTextString(obj.AsString());
                break;

            case CBORType.ByteString:
                writer.WriteByteString(obj.GetByteString());
                break;

            case CBORType.Integer:
                writer.WriteInt64(obj.AsNumber().ToInt64Checked());
                break;

            case CBORType.FloatingPoint:
                writer.WriteDouble(obj.AsDouble());
                break;

            case CBORType.Boolean:
                writer.WriteBoolean(obj.AsBoolean());
                break;

            default:
                if (obj.IsNull || obj.IsUndefined)
                {
                    writer.WriteNull();
                }
                else
                {
                    throw new NotSupportedException($"CBORType.{obj.Type} is not supported");
                }
                break;
        }
    }

    private static async Task<byte[]?> ReadBlockBytesAsync(string dbPath, string cid)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Content FROM repo_block WHERE Cid = @cid";
        cmd.Parameters.AddWithValue("@cid", cid);
        var result = await cmd.ExecuteScalarAsync();
        return result as byte[];
    }

    private static async Task<RepoResyncJobState> WaitForCompletionAsync(RepoResyncService service)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var state = service.GetStatus();
            if (state.Status == RepoResyncStatus.Completed)
            {
                return state;
            }
            if (state.Status == RepoResyncStatus.Failed)
            {
                throw new Exception($"Repo resync failed: {state.Error}");
            }
            await Task.Delay(200);
        }
        throw new TimeoutException("Repo resync did not complete in time");
    }

    [Test]
    [NotInParallel]
    public async Task Resync_RepairsDivergentBlockBytes()
    {
        var (account, uri, _) = await CreatePostAsync();
        var dbPath = GetActorDbPath(account.Did);
        var recordCid = await GetRecordIndexCidAsync(dbPath, uri);

        var (nonCanonicalBytes, canonicalBytes) = await InjectNonCanonicalBlockAsync(dbPath, recordCid);

        var storedBefore = await ReadBlockBytesAsync(dbPath, recordCid);
        await Assert.That(storedBefore).IsNotNull();
        await Assert.That(storedBefore!.SequenceEqual(nonCanonicalBytes)).IsTrue();

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RepoResyncService>();
        await service.StartAsync(account.Did);

        var state = await WaitForCompletionAsync(service);
        await Assert.That(state.Status).IsEqualTo(RepoResyncStatus.Completed);
        await Assert.That(state.Error).IsNull();
        await Assert.That(state.RecordsScanned).IsGreaterThanOrEqualTo(1);
        await Assert.That(state.RecordsRewritten).IsGreaterThanOrEqualTo(1);

        var storedAfter = await ReadBlockBytesAsync(dbPath, recordCid);
        await Assert.That(storedAfter).IsNotNull();
        await Assert.That(storedAfter!.SequenceEqual(canonicalBytes)).IsTrue();
        await Assert.That(storedAfter.SequenceEqual(nonCanonicalBytes)).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task Resync_RebuildsRepo_AndGetRecordStillWorks()
    {
        var (account, uri, _) = await CreatePostAsync();
        var dbPath = GetActorDbPath(account.Did);
        var recordCid = await GetRecordIndexCidAsync(dbPath, uri);

        var (nonCanonicalBytes, canonicalBytes) = await InjectNonCanonicalBlockAsync(dbPath, recordCid);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RepoResyncService>();
        await service.StartAsync(account.Did);
        var state = await WaitForCompletionAsync(service);
        await Assert.That(state.Status).IsEqualTo(RepoResyncStatus.Completed);

        var rkey = uri.Split('/').Last();
        var getResponse = await Client.GetAsync(
            $"/xrpc/com.atproto.repo.getRecord?repo={account.Did}&collection=app.bsky.feed.post&rkey={rkey}");
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(getResponse);
        await Assert.That(json.GetProperty("uri").GetString()).IsEqualTo(uri);
        await Assert.That(json.GetProperty("cid").GetString()).IsEqualTo(recordCid);

        var storedAfter = await ReadBlockBytesAsync(dbPath, recordCid);
        await Assert.That(storedAfter).IsNotNull();
        await Assert.That(storedAfter!.SequenceEqual(canonicalBytes)).IsTrue();
        await Assert.That(storedAfter.SequenceEqual(nonCanonicalBytes)).IsFalse();
    }

    private static async Task<string> GetRootCidAsync(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Cid FROM repo_root";
        var result = await cmd.ExecuteScalarAsync();
        return result as string ?? throw new InvalidOperationException("No repo root");
    }

    private static async Task<string> GetDataRootCidAsync(string dbPath, string commitCid)
    {
        var bytes = await ReadBlockBytesAsync(dbPath, commitCid)
            ?? throw new InvalidOperationException($"Block {commitCid} not found");
        var commit = Commit.FromCborObject(CBORObject.DecodeFromBytes(bytes));
        return commit.Data.ToString();
    }

    private static async Task DeleteBlockAsync(string dbPath, string cid)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM repo_block WHERE Cid = @cid";
        cmd.Parameters.AddWithValue("@cid", cid);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<RepoResyncJobState> WaitForTerminalStateAsync(RepoResyncService service)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var state = service.GetStatus();
            if (state.Status is RepoResyncStatus.Completed or RepoResyncStatus.Failed)
            {
                return state;
            }
            await Task.Delay(200);
        }
        throw new TimeoutException("Repo resync did not reach a terminal state in time");
    }

    private async Task<HttpResponseMessage> DeletePostAsync(AccountInfo account, string rkey)
    {
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
        return await Client.SendAsync(request);
    }

    [Test]
    [NotInParallel]
    public async Task Resync_RecoversRecordsFromIndex_WhenMstRootMissing()
    {
        var (account, uri, _) = await CreatePostAsync();
        var rkey = uri.Split('/').Last();
        var dbPath = GetActorDbPath(account.Did);
        var recordCid = await GetRecordIndexCidAsync(dbPath, uri);
        var rootBefore = await GetRootCidAsync(dbPath);

        // Corrupt the MST data-root block; index rows and leaf blocks survive.
        var dataRoot = await GetDataRootCidAsync(dbPath, rootBefore);
        await DeleteBlockAsync(dbPath, dataRoot);

        // Writes now fail with a mapped, retryable error instead of a raw 500.
        var brokenDelete = await DeletePostAsync(account, rkey);
        await Assert.That(brokenDelete.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await brokenDelete.Content.ReadAsStringAsync()).Contains("RepoCorrupted");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RepoResyncService>();
        await service.StartAsync(account.Did);
        var state = await WaitForTerminalStateAsync(service);

        await Assert.That(state.Status).IsEqualTo(RepoResyncStatus.Completed);
        await Assert.That(state.Error).IsNull();
        await Assert.That(state.SkippedRecords.Count).IsEqualTo(0);
        await Assert.That(state.RecordsScanned).IsGreaterThanOrEqualTo(1);

        // Root moved to a fresh, complete tree.
        var rootAfter = await GetRootCidAsync(dbPath);
        await Assert.That(rootAfter == rootBefore).IsFalse();

        // The recovered post is readable under its original CID and deletable again.
        var getResponse = await Client.GetAsync(
            $"/xrpc/com.atproto.repo.getRecord?repo={account.Did}&collection=app.bsky.feed.post&rkey={rkey}");
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(getResponse);
        await Assert.That(json.GetProperty("cid").GetString()).IsEqualTo(recordCid);

        var deleteResponse = await DeletePostAsync(account, rkey);
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    [NotInParallel]
    public async Task Resync_FailsLoudly_WhenIndexedBlockTrulyMissing()
    {
        var (account, uri, _) = await CreatePostAsync();
        var dbPath = GetActorDbPath(account.Did);
        var recordCid = await GetRecordIndexCidAsync(dbPath, uri);
        var rootBefore = await GetRootCidAsync(dbPath);

        // Destroy both the MST data-root and the leaf block: unrecoverable.
        var dataRoot = await GetDataRootCidAsync(dbPath, rootBefore);
        await DeleteBlockAsync(dbPath, dataRoot);
        await DeleteBlockAsync(dbPath, recordCid);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RepoResyncService>();
        await service.StartAsync(account.Did);
        var state = await WaitForTerminalStateAsync(service);

        await Assert.That(state.Status).IsEqualTo(RepoResyncStatus.Failed);
        await Assert.That(state.SkippedRecords.Contains(uri)).IsTrue();
        await Assert.That(state.Error).IsNotNull();

        // No empty rewrite was committed and no user data was GC'd.
        await Assert.That(await GetRootCidAsync(dbPath)).IsEqualTo(rootBefore);
        await Assert.That(await GetRecordIndexCidAsync(dbPath, uri)).IsEqualTo(recordCid);
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

    private async Task<(AccountInfo account, string blobCid, string uri)> CreatePostWithBlobAsync()
    {
        var account = await CreateAccountAsync();
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
        var uploadJson = await AuthTestHelper.ReadJsonAsync(uploadResponse);
        var blobCid = uploadJson.GetProperty("blob").GetProperty("ref").GetProperty("$link").GetString()!;

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
                                ["ref"] = new Dictionary<string, object?> { ["$link"] = blobCid },
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
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var createJson = await AuthTestHelper.ReadJsonAsync(createResponse);
        return (account, blobCid, createJson.GetProperty("uri").GetString()!);
    }

    private static async Task<int> CountRecordBlobsAsync(string dbPath, string uri)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM record_blob WHERE RecordUri = @uri";
        cmd.Parameters.AddWithValue("@uri", uri);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task WipeRecordBlobsAsync(string dbPath, string uri)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM record_blob WHERE RecordUri = @uri";
        cmd.Parameters.AddWithValue("@uri", uri);
        await cmd.ExecuteNonQueryAsync();
    }

    [Test]
    [NotInParallel]
    public async Task Resync_ReconcilesBlobReferences()
    {
        var (account, blobCid, uri) = await CreatePostWithBlobAsync();
        var rkey = uri.Split('/').Last();
        var dbPath = GetActorDbPath(account.Did);

        await Assert.That(await CountRecordBlobsAsync(dbPath, uri)).IsEqualTo(1);

        // Simulate index drift: blob link lost, blob row/file still present.
        await WipeRecordBlobsAsync(dbPath, uri);
        await Assert.That(await CountRecordBlobsAsync(dbPath, uri)).IsEqualTo(0);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RepoResyncService>();
        await service.StartAsync(account.Did);
        var state = await WaitForTerminalStateAsync(service);
        await Assert.That(state.Status).IsEqualTo(RepoResyncStatus.Completed);

        await Assert.That(await CountRecordBlobsAsync(dbPath, uri)).IsEqualTo(1);

        // Deleting the post cleans the reconciled link through the normal path.
        var deleteResponse = await DeletePostAsync(account, rkey);
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await CountRecordBlobsAsync(dbPath, uri)).IsEqualTo(0);
    }

    [Test]
    public async Task Endpoints_NoAuth_ReturnsAuthError()
    {
        var create = await Client.PostAsync("/api/admin/repo/resync", new StringContent("{\"did\":\"did:plc:test\"}", Encoding.UTF8, "application/json"));
        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var status = await Client.GetAsync("/api/admin/repo/resync/status");
        await Assert.That(status.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
