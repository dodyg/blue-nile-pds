using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class AdminLifecycleTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private HttpRequestMessage CreateAdminRequest(string method, string url, string? body = null)
    {
        var httpMethod = method == "GET" ? HttpMethod.Get : HttpMethod.Post;
        var request = new HttpRequestMessage(httpMethod, url);
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        else if (httpMethod == HttpMethod.Post)
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return request;
    }

    [Test]
    public async Task GetAccountInfo_ReturnsAccount()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = CreateAdminRequest("GET", $"/xrpc/com.atproto.admin.getAccountInfo?did={account.Did}");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("did").GetString()).IsEqualTo(account.Did);
        await Assert.That(json.GetProperty("handle").GetString()).IsNotNull();
        await Assert.That(json.GetProperty("email").GetString()).IsNotNull();
    }

    [Test]
    public async Task GetAccountInfos_ReturnsAccounts()
    {
        var account1 = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
        var account2 = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = CreateAdminRequest("GET",
            $"/xrpc/com.atproto.admin.getAccountInfos?dids={account1.Did},{account2.Did}");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var accounts = json.GetProperty("accounts").EnumerateArray().ToList();
        await Assert.That(accounts.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task AdminUpdateAccountEmail()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
        var newEmail = UniqueEmail();

        var body = JsonSerializer.Serialize(new Dictionary<string, object?> { ["did"] = account.Did, ["email"] = newEmail });
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.updateAccountEmail", body);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var infoRequest = CreateAdminRequest("GET", $"/xrpc/com.atproto.admin.getAccountInfo?did={account.Did}");
        var infoResponse = await Client.SendAsync(infoRequest);
        var json = await AuthTestHelper.ReadJsonAsync(infoResponse);
        await Assert.That(json.GetProperty("email").GetString()).IsEqualTo(newEmail);
    }

    [Test]
    public async Task AdminUpdateAccountHandle()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
        var newHandle = UniqueHandle();

        var body = JsonSerializer.Serialize(new Dictionary<string, object?> { ["did"] = account.Did, ["handle"] = newHandle });
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.updateAccountHandle", body);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task AdminDeleteAccount()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var body = JsonSerializer.Serialize(new Dictionary<string, object?> { ["did"] = account.Did });
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.deleteAccount", body);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var infoRequest = CreateAdminRequest("GET", $"/xrpc/com.atproto.admin.getAccountInfo?did={account.Did}");
        var infoResponse = await Client.SendAsync(infoRequest);
        await Assert.That(infoResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Takedown_Account()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var body = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["subject"] = new Dictionary<string, object?> { ["$type"] = "com.atproto.admin.repoRef", ["did"] = account.Did },
            ["takedown"] = new Dictionary<string, object?> { ["applied"] = true }
        });
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.updateSubjectStatus", body);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetSubjectStatus_ReturnsStatus()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = CreateAdminRequest("GET", $"/xrpc/com.atproto.admin.getSubjectStatus?did={account.Did}");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task EnableInvites_ForAccount()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var body = JsonSerializer.Serialize(new Dictionary<string, object?> { ["did"] = account.Did });
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.enableAccountInvites", body);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DisableInvites_ForAccount()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var body = JsonSerializer.Serialize(new Dictionary<string, object?> { ["did"] = account.Did });
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.disableAccountInvites", body);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetInviteCodes_ReturnsCodes()
    {
        var request = CreateAdminRequest("GET", "/xrpc/com.atproto.admin.getInviteCodes");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("codes", out _)).IsTrue();
    }

    [Test]
    public async Task DisableInviteCodes()
    {
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.disableInviteCodes",
            """{"codes":["nonexistent-code"]}""");
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task SendEmail_ToAccount()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var body = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["recipientDid"] = account.Did,
            ["content"] = "Test email body",
            ["subject"] = "Test subject"
        });
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.sendEmail", body);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
