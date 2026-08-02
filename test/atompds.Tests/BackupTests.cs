using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using atompds.Services;
using atompds.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xrpc;

namespace atompds.Tests;

public class BackupTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private HttpRequestMessage CreateAdminRequest(HttpMethod method, string url, string? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        if (body != null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        return request;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private async Task<HttpResponseMessage> GetAsync(string url)
    {
        return await Client.SendAsync(CreateAdminRequest(HttpMethod.Get, url));
    }

    private async Task<JsonElement> CreateAndWaitForCompletionAsync()
    {
        var createRequest = CreateAdminRequest(HttpMethod.Post, "/admin/api/backup/create");
        var createResponse = await Client.SendAsync(createRequest);
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var statusResponse = await GetAsync("/admin/api/backup/status");
            var status = await ReadJsonAsync(statusResponse);
            var state = status.GetProperty("status").GetString();
            if (state == "completed")
            {
                return status;
            }
            if (state == "failed")
            {
                throw new Exception($"Backup failed: {status.GetProperty("error").GetString()}");
            }
            await Task.Delay(100);
        }

        throw new TimeoutException("Backup did not complete in time");
    }

    [Test]
    public async Task Endpoints_NoAuth_ReturnsAuthError()
    {
        var create = await Client.PostAsync("/admin/api/backup/create", null);
        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var status = await Client.GetAsync("/admin/api/backup/status");
        await Assert.That(status.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var list = await Client.GetAsync("/admin/api/backup/list");
        await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var download = await Client.GetAsync("/admin/api/backup/download?fileName=backup-x.zip");
        await Assert.That(download.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        var delete = await Client.PostAsync("/admin/api/backup/delete", null);
        await Assert.That(delete.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel]
    public async Task CreateBackup_Completes_AndDownloadContainsExpectedFiles()
    {
        var status = await CreateAndWaitForCompletionAsync();
        var fileName = status.GetProperty("fileName").GetString();
        await Assert.That(fileName).IsNotNull().And.EndsWith(".zip");

        var listResponse = await GetAsync("/admin/api/backup/list");
        await Assert.That(listResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await ReadJsonAsync(listResponse);
        var matches = list.GetProperty("backups").EnumerateArray()
            .Where(b => b.GetProperty("fileName").GetString() == fileName);
        await Assert.That(matches.Count()).IsEqualTo(1);

        var downloadRequest = CreateAdminRequest(HttpMethod.Get, $"/admin/api/backup/download?fileName={fileName}");
        var downloadResponse = await Client.SendAsync(downloadRequest);
        await Assert.That(downloadResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(downloadResponse.Content.Headers.ContentType?.MediaType).IsEqualTo("application/zip");

        await using var stream = await downloadResponse.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entries = archive.Entries.Select(e => e.FullName).ToList();
        await Assert.That(entries).Contains("manifest.json");
        await Assert.That(entries).Contains("account.sqlite");
        await Assert.That(entries).Contains("sequencer.sqlite");

        var manifestEntry = archive.GetEntry("manifest.json");
        await Assert.That(manifestEntry).IsNotNull();
        using var manifestReader = new StreamReader(manifestEntry!.Open());
        using var manifestDoc = JsonDocument.Parse(await manifestReader.ReadToEndAsync());
        await Assert.That(manifestDoc.RootElement.GetProperty("backupFile").GetString()).IsEqualTo(fileName);
    }

    [Test]
    public async Task Download_InvalidFileName_ReturnsBadRequest()
    {
        var request = CreateAdminRequest(HttpMethod.Get, "/admin/api/backup/download?fileName=../secret.zip");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var request2 = CreateAdminRequest(HttpMethod.Get, "/admin/api/backup/download?fileName=notazip.txt");
        var response2 = await Client.SendAsync(request2);
        await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Download_UnknownFile_ReturnsBadRequest()
    {
        var request = CreateAdminRequest(HttpMethod.Get, "/admin/api/backup/download?fileName=does-not-exist.zip");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [NotInParallel]
    public async Task DeleteBackup_RemovesFileFromList()
    {
        var status = await CreateAndWaitForCompletionAsync();
        var fileName = status.GetProperty("fileName").GetString();

        var deleteRequest = CreateAdminRequest(HttpMethod.Post, "/admin/api/backup/delete", JsonSerializer.Serialize(new { fileName }));
        var deleteResponse = await Client.SendAsync(deleteRequest);
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        var listResponse = await GetAsync("/admin/api/backup/list");
        var list = await ReadJsonAsync(listResponse);
        var matches = list.GetProperty("backups").EnumerateArray()
            .Where(b => b.GetProperty("fileName").GetString() == fileName);
        await Assert.That(matches.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteBackup_InvalidFileName_ReturnsBadRequest()
    {
        var deleteRequest = CreateAdminRequest(HttpMethod.Post, "/admin/api/backup/delete", "{\"fileName\":\"../evil.zip\"}");
        var deleteResponse = await Client.SendAsync(deleteRequest);
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [NotInParallel]
    public async Task CreateBackup_WhileRunning_ReturnsBackupAlreadyRunning()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BackupService>();

        await service.CreateBackupAsync();
        await Assert.ThrowsAsync<XRPCError>(() => service.CreateBackupAsync().AsTask());

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && service.GetStatus().Status == BackupStatus.Running)
        {
            await Task.Delay(50);
        }
    }
}
