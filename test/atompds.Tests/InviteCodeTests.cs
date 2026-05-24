using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class InviteCodeTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    [Test]
    public async Task CreateInviteCode_Succeeds()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
        var code = await AccountHelper.CreateInviteCodeAsync(Client, account);
        await Assert.That(code).IsNotNull();
        await Assert.That(code.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task CreateInviteCodes_MultipleCodes()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.createInviteCodes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        request.Content = new StringContent(
            """{"codeCount": 3, "useCount": 1}""", System.Text.Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var codes = json.GetProperty("codes").EnumerateArray().ToList();
        await Assert.That(codes.Count).IsEqualTo(3);
    }

    [Test]
    public async Task CreateAccount_WithValidCode_Succeeds()
    {
        var creator = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
        var code = await AccountHelper.CreateInviteCodeAsync(Client, creator);

        var account = await AccountHelper.CreateAccountAsync(Client,
            handle: UniqueHandle(), email: UniqueEmail(), inviteCode: code);

        await Assert.That(account.Did).IsNotNull();
    }

    [Test]
    public async Task GetAccountInviteCodes_ReturnsCodes()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
        await AccountHelper.CreateInviteCodeAsync(Client, account);

        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.server.getAccountInviteCodes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        var codes = json.GetProperty("codes").EnumerateArray().ToList();
        await Assert.That(codes.Count).IsGreaterThan(0);
    }
}
