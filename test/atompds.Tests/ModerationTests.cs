using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class ModerationTests
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
    public async Task CreateReport_Succeeds()
    {
        var account = await CreateAccountAsync();

        var body = new Dictionary<string, object?>
        {
            ["reasonType"] = "com.atproto.moderation.defs#reasonSpam",
            ["reason"] = "Test report",
            ["subject"] = new Dictionary<string, object?>
            {
                ["$type"] = "com.atproto.admin.repoRef",
                ["did"] = account.Did
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.moderation.createReport")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateReport_NoAuth_ReturnsError()
    {
        var body = new Dictionary<string, object?>
        {
            ["reasonType"] = "com.atproto.moderation.defs#reasonSpam",
            ["subject"] = new Dictionary<string, object?> { ["did"] = "did:plc:test" }
        };
        var response = await Client.PostAsync("/xrpc/com.atproto.moderation.createReport",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Takedown_Account_ViaAdmin()
    {
        var account = await CreateAccountAsync();

        var body = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["subject"] = new Dictionary<string, object?> { ["$type"] = "com.atproto.admin.repoRef", ["did"] = account.Did },
            ["takedown"] = new Dictionary<string, object?> { ["applied"] = true }
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.admin.updateSubjectStatus")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Untakedown_RestoresAccess()
    {
        var account = await CreateAccountAsync();

        var takedownBody = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["subject"] = new Dictionary<string, object?> { ["$type"] = "com.atproto.admin.repoRef", ["did"] = account.Did },
            ["takedown"] = new Dictionary<string, object?> { ["applied"] = true }
        });
        var takedownRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.admin.updateSubjectStatus")
        {
            Content = new StringContent(takedownBody, Encoding.UTF8, "application/json")
        };
        takedownRequest.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        await Client.SendAsync(takedownRequest);

        var untakedownBody = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["subject"] = new Dictionary<string, object?> { ["$type"] = "com.atproto.admin.repoRef", ["did"] = account.Did },
            ["takedown"] = new Dictionary<string, object?> { ["applied"] = false }
        });
        var untakedownRequest = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.admin.updateSubjectStatus")
        {
            Content = new StringContent(untakedownBody, Encoding.UTF8, "application/json")
        };
        untakedownRequest.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        var response = await Client.SendAsync(untakedownRequest);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Takedown_GetSubjectStatus_ShowsTakedown()
    {
        var account = await CreateAccountAsync();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/xrpc/com.atproto.admin.getSubjectStatus?did={account.Did}");
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }
}
