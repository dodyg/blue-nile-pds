using System.Net;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace atompds.Tests;

public class AccountApprovalTests
{
    private static readonly TestWebAppFactory Factory = new(new Dictionary<string, string?>
    {
        ["Config:PDS_ACCOUNT_APPROVAL_REQUIRED"] = "true"
    });
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<(string Did, string Handle, string Email, string Password, string AccessJwt)> CreatePendingAccountAsync(
        string? location = null,
        string? accountType = null)
    {
        var handle = UniqueHandle();
        var email = UniqueEmail();
        var password = "test-password-123";

        var body = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["handle"] = handle,
            ["password"] = password
        };
        var response = await Client.SendAsync(AuthTestHelper.CreateJsonRequest(HttpMethod.Post, "/xrpc/com.atproto.server.createAccount", body));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);

        var did = json.GetProperty("did").GetString()!;
        var accessJwt = json.GetProperty("accessJwt").GetString()!;
        await Assert.That(string.IsNullOrEmpty(accessJwt)).IsFalse();

        if (!string.IsNullOrWhiteSpace(location) || !string.IsNullOrWhiteSpace(accountType))
        {
            await SetAccountProfileAsync(accessJwt, location, accountType);
        }

        return (did, handle, email, password, accessJwt);
    }

    private async Task SetAccountProfileAsync(string accessJwt, string? location, string? accountType)
    {
        var body = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(location)) body["location"] = location;
        if (!string.IsNullOrWhiteSpace(accountType)) body["accountType"] = accountType;

        var request = AuthTestHelper.CreateJsonRequest(HttpMethod.Post, "/xrpc/africa.bsky.setAccountProfile", body);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("uri").GetString()).IsNotNull();
        await Assert.That(json.GetProperty("cid").GetString()).IsNotNull();
    }

    private static HttpRequestMessage AdminRequest(HttpMethod method, string url, object? body = null)
    {
        var request = body == null
            ? new HttpRequestMessage(method, url)
            : AuthTestHelper.CreateJsonRequest(method, url, body);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret")));
        return request;
    }

    private async Task<(bool Active, string Status)> SessionStatusAsync(string identifier, string password)
    {
        var body = new Dictionary<string, object?>
        {
            ["identifier"] = identifier,
            ["password"] = password
        };
        var request = AuthTestHelper.CreateJsonRequest(HttpMethod.Post, "/xrpc/com.atproto.server.createSession", body);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        return (json.GetProperty("active").GetBoolean(), json.GetProperty("status").GetString()!);
    }

    [Test]
    public async Task PendingAccount_CanLogIn_ReportsSuspended()
    {
        var (_, handle, _, password, _) = await CreatePendingAccountAsync();

        var (active, status) = await SessionStatusAsync(handle, password);
        await Assert.That(active).IsFalse();
        await Assert.That(status).IsEqualTo("Suspended");
    }

    [Test]
    public async Task PendingAccount_ListsInAdminQueue()
    {
        var (did, _, _, _, _) = await CreatePendingAccountAsync();

        var response = await Client.SendAsync(AdminRequest(HttpMethod.Get, "/xrpc/africa.bsky.admin.listPendingAccounts?limit=100"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var pending = json.GetProperty("accounts").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("did").GetString() == did);
        await Assert.That(pending.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(pending.GetProperty("emailConfirmed").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task SetAccountProfile_PersistsLocationAndType()
    {
        var (did, _, _, _, _) = await CreatePendingAccountAsync(location: "Lagos, Nigeria", accountType: "business");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
        var account = await db.Accounts.FindAsync(did);
        var actor = await db.Actors.FindAsync(did);

        await Assert.That(account).IsNotNull();
        await Assert.That(account!.Location).IsEqualTo("Lagos, Nigeria");
        await Assert.That(account.AccountType).IsEqualTo("business");
        await Assert.That(actor!.SuspendedAt).IsNotNull();
    }

    [Test]
    public async Task PendingAccount_LocationAndType_VisibleInAdminQueue()
    {
        var (did, _, _, _, _) = await CreatePendingAccountAsync(location: "Nairobi, Kenya", accountType: "organization");

        var response = await Client.SendAsync(AdminRequest(HttpMethod.Get, "/xrpc/africa.bsky.admin.listPendingAccounts?limit=100"));
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var pending = json.GetProperty("accounts").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("did").GetString() == did);

        await Assert.That(pending.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(pending.GetProperty("location").GetString()).IsEqualTo("Nairobi, Kenya");
        await Assert.That(pending.GetProperty("accountType").GetString()).IsEqualTo("organization");
    }

    [Test]
    public async Task PendingAccount_EmailConfirmed_VisibleInAdminQueue()
    {
        var (did, _, _, _, _) = await CreatePendingAccountAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccountManager.Db.AccountManagerDb>();
            var account = await db.Accounts.FindAsync(did);
            await Assert.That(account).IsNotNull();
            account!.EmailConfirmedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var response = await Client.SendAsync(AdminRequest(HttpMethod.Get, "/xrpc/africa.bsky.admin.listPendingAccounts?limit=100"));
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var pending = json.GetProperty("accounts").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("did").GetString() == did);

        await Assert.That(pending.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(pending.GetProperty("emailConfirmed").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task SuspendedAccount_CannotWriteRecords()
    {
        var (did, _, _, _, accessJwt) = await CreatePendingAccountAsync();

        var body = new Dictionary<string, object?>
        {
            ["repo"] = did,
            ["collection"] = "app.bsky.feed.post",
            ["rkey"] = "test",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = "hello",
                ["createdAt"] = DateTime.UtcNow.ToString("O")
            }
        };
        var request = AuthTestHelper.CreateJsonRequest(HttpMethod.Post, "/xrpc/com.atproto.repo.putRecord", body);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessJwt);
        var response = await Client.SendAsync(request);

        await AuthTestHelper.AssertXrpcErrorAsync(response, HttpStatusCode.Forbidden, "AccountSuspended");
    }

    [Test]
    public async Task ApproveAccount_ActivatesAccount()
    {
        var (did, handle, _, password, _) = await CreatePendingAccountAsync();

        var approveResponse = await Client.SendAsync(AdminRequest(HttpMethod.Post, "/xrpc/africa.bsky.admin.approveAccount", new Dictionary<string, object?> { ["did"] = did }));
        await Assert.That(approveResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var (active, status) = await SessionStatusAsync(handle, password);
        await Assert.That(active).IsTrue();
        await Assert.That(status).IsEqualTo("Active");
    }

    [Test]
    public async Task ApprovedAccount_RemovedFromPendingQueue()
    {
        var (did, _, _, _, _) = await CreatePendingAccountAsync();

        await Client.SendAsync(AdminRequest(HttpMethod.Post, "/xrpc/africa.bsky.admin.approveAccount", new Dictionary<string, object?> { ["did"] = did }));

        var response = await Client.SendAsync(AdminRequest(HttpMethod.Get, "/xrpc/africa.bsky.admin.listPendingAccounts?limit=100"));
        var json = await AuthTestHelper.ReadJsonAsync(response);

        await Assert.That(json.GetProperty("accounts").EnumerateArray()
            .All(a => a.GetProperty("did").GetString() != did)).IsTrue();
    }

    [Test]
    public async Task RejectAccount_KeepsAccountSuspended()
    {
        var (did, handle, _, password, _) = await CreatePendingAccountAsync();

        var rejectResponse = await Client.SendAsync(AdminRequest(HttpMethod.Post, "/xrpc/africa.bsky.admin.rejectAccount", new Dictionary<string, object?> { ["did"] = did }));
        await Assert.That(rejectResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var (active, status) = await SessionStatusAsync(handle, password);
        await Assert.That(active).IsFalse();
        await Assert.That(status).IsEqualTo("Suspended");
    }

    [Test]
    public async Task ApprovedAccount_VisibleInListRepos_AsSuspendedBefore()
    {
        var (did, _, _, _, _) = await CreatePendingAccountAsync();

        var listResponse = await Client.GetAsync("/xrpc/com.atproto.sync.listRepos?limit=1000");
        await Assert.That(listResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var listJson = await AuthTestHelper.ReadJsonAsync(listResponse);
        var entry = listJson.GetProperty("repos").EnumerateArray()
            .FirstOrDefault(r => r.GetProperty("did").GetString() == did);

        // While pending the account should already be listed as inactive/suspended.
        await Assert.That(entry.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(entry.GetProperty("status").GetString()).IsEqualTo("Suspended");
    }
}