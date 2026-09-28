using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BlueNilePds.Host.Tests.Infrastructure;

namespace BlueNilePds.Host.Tests;

public class SequencerAdminTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private static HttpRequestMessage AdminRequest(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        if (body != null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        return request;
    }

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
                ["text"] = "sequencer admin test",
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
    }

    private async Task<JsonElement> GetStatusAsync()
    {
        var response = await Client.SendAsync(AdminRequest(HttpMethod.Get, "/api/admin/sequencer/status"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return await AuthTestHelper.ReadJsonAsync(response);
    }

    [Test]
    public async Task Status_ReturnsSequenceInfoAsync()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);

        var status = await GetStatusAsync();
        await Assert.That(status.GetProperty("eventCount").GetInt32()).IsGreaterThanOrEqualTo(1);
        await Assert.That(status.GetProperty("currentSeq").GetInt32()).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Advance_MovesCounterForwardAsync()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);
        var before = await GetStatusAsync();
        var current = before.GetProperty("currentSeq").GetInt32();
        var target = current + 100;

        var response = await Client.SendAsync(AdminRequest(HttpMethod.Post, "/api/admin/sequencer/advance",
            new Dictionary<string, object?> { ["targetSeq"] = target }));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("previousSeq").GetInt32()).IsEqualTo(current);
        await Assert.That(json.GetProperty("newSeq").GetInt32()).IsEqualTo(target);

        await CreatePostAsync(account);
        var after = await GetStatusAsync();
        await Assert.That(after.GetProperty("currentSeq").GetInt32()).IsEqualTo(target + 1);
    }

    [Test]
    public async Task Advance_Backwards_ReturnsBadRequestAsync()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);
        var before = await GetStatusAsync();
        var current = before.GetProperty("currentSeq").GetInt32();

        var response = await Client.SendAsync(AdminRequest(HttpMethod.Post, "/api/admin/sequencer/advance",
            new Dictionary<string, object?> { ["targetSeq"] = current }));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Advance_RequiresAdminAuthAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/sequencer/advance")
        {
            Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, object?> { ["targetSeq"] = 999999 }), Encoding.UTF8, "application/json")
        };
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }
}
