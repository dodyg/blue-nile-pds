using System.Text;
using System.Text.Json;

namespace atompds.Tests.Infrastructure;

public static class AccountHelper
{
    private static int _counter;

    public static async Task<AccountInfo> CreateAccountAsync(
        HttpClient client,
        string? email = null,
        string? handle = null,
        string? password = null,
        string? inviteCode = null)
    {
        var id = Interlocked.Increment(ref _counter);
        email ??= $"test{id}@test.test";
        handle ??= $"testuser{id}.test";
        password ??= "test-password-123";

        var body = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["handle"] = handle,
            ["password"] = password,
        };
        if (inviteCode != null)
            body["inviteCode"] = inviteCode;

        var request = new HttpRequestMessage(HttpMethod.Post,
            "/xrpc/com.atproto.server.createAccount")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await AuthTestHelper.ReadJsonAsync(response);

        return new AccountInfo(
            Did: json.GetProperty("did").GetString()!,
            Handle: handle,
            Email: email,
            Password: password,
            AccessJwt: json.GetProperty("accessJwt").GetString()!,
            RefreshJwt: json.GetProperty("refreshJwt").GetString()!,
            Active: json.TryGetProperty("active", out var active) && active.GetBoolean()
        );
    }

    public static async Task<string> CreateInviteCodeAsync(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            "/xrpc/com.atproto.server.createInviteCode");
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        request.Content = new StringContent(
            """{"useCount": 1}""", Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await AuthTestHelper.ReadJsonAsync(response);
        return json.GetProperty("code").GetString()!;
    }

    public static async Task<string> CreateInviteCodesAsync(HttpClient client, int codeCount = 2, int useCount = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            "/xrpc/com.atproto.server.createInviteCodes");
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        request.Content = new StringContent(
            $"{{\"codeCount\": {codeCount}, \"useCount\": {useCount}}}", Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await AuthTestHelper.ReadJsonAsync(response);
        return json.GetProperty("codes").EnumerateArray().First().GetString()!;
    }
}

public record AccountInfo(
    string Did, string Handle, string Email, string? Password,
    string AccessJwt, string RefreshJwt, bool Active);
