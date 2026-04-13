using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using Jose;

namespace atompds.Tests.Infrastructure;

public static class AuthTestHelper
{
    public static string CreateAccessToken(
        string did = TestWebAppFactory.TestDid,
        string scope = "com.atproto.access",
        string? audience = null)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["scope"] = scope,
            ["sub"] = did,
            ["aud"] = audience ?? TestWebAppFactory.ServiceDid,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddHours(2).ToUnixTimeSeconds()
        };

        var headers = new Dictionary<string, object>
        {
            ["typ"] = "at+jwt"
        };

        return JWT.Encode(payload, Encoding.UTF8.GetBytes(TestWebAppFactory.JwtSecret), JwsAlgorithm.HS256, headers);
    }

    public static string CreateRefreshToken(
        string did = TestWebAppFactory.TestDid,
        string? jti = null)
    {
        var now = DateTimeOffset.UtcNow;
        jti ??= Guid.NewGuid().ToString();

        var payload = new Dictionary<string, object>
        {
            ["scope"] = "com.atproto.refresh",
            ["sub"] = did,
            ["aud"] = TestWebAppFactory.ServiceDid,
            ["jti"] = jti,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddDays(90).ToUnixTimeSeconds()
        };

        var headers = new Dictionary<string, object>
        {
            ["typ"] = "refresh+jwt"
        };

        return JWT.Encode(payload, Encoding.UTF8.GetBytes(TestWebAppFactory.JwtSecret), JwsAlgorithm.HS256, headers);
    }

    public static string CreateAppPasswordToken(
        string did = TestWebAppFactory.TestDid,
        string scope = "com.atproto.appPass")
    {
        return CreateAccessToken(did, scope);
    }

    public static string CreatePrivilegedToken(
        string did = TestWebAppFactory.TestDid)
    {
        return CreateAccessToken(did, "com.atproto.appPassPrivileged");
    }

    public static string GetAdminBasicAuth()
    {
        var credentials = $"{TestWebAppFactory.AdminUser}:{TestWebAppFactory.AdminPassword}";
        return $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials))}";
    }

    public static string GetBearerAuth(string token)
    {
        return $"Bearer {token}";
    }

    public static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
