using System.Security.Cryptography;
using System.Text;
using Jose;

namespace BlueNilePds.Pds.PendingAccounts.Services;

public class PendingJwtService
{
    private const string ACCESS_TOKEN_SCOPE = "pending.access";
    private const string REFRESH_TOKEN_SCOPE = "pending.refresh";

    private readonly string _jwtSecret;
    private readonly string _serviceDid;

    public PendingJwtService(string jwtSecret, string serviceDid)
    {
        _jwtSecret = jwtSecret;
        _serviceDid = serviceDid;
    }

    public (string AccessToken, string RefreshToken) CreateTokens(int pendingRegistrationId, TimeSpan? expiresIn = null)
    {
        var access = CreateAccessToken(pendingRegistrationId, expiresIn);
        var refresh = CreateRefreshToken(pendingRegistrationId);
        return (access, refresh);
    }

    public string CreateAccessToken(int pendingRegistrationId, TimeSpan? expiresIn = null)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["scope"] = ACCESS_TOKEN_SCOPE,
            ["sub"] = pendingRegistrationId.ToString(),
            ["aud"] = _serviceDid,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(expiresIn ?? TimeSpan.FromHours(24)).ToUnixTimeSeconds()
        };
        var headers = new Dictionary<string, object> { ["typ"] = "at+jwt" };
        return JWT.Encode(payload, GetKey(), JwsAlgorithm.HS256, headers);
    }

    public string CreateRefreshToken(int pendingRegistrationId)
    {
        var now = DateTimeOffset.UtcNow;
        var jti = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var payload = new Dictionary<string, object>
        {
            ["scope"] = REFRESH_TOKEN_SCOPE,
            ["sub"] = pendingRegistrationId.ToString(),
            ["aud"] = _serviceDid,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddDays(30).ToUnixTimeSeconds(),
            ["jti"] = jti
        };
        var headers = new Dictionary<string, object> { ["typ"] = "refresh+jwt" };
        return JWT.Encode(payload, GetKey(), JwsAlgorithm.HS256, headers);
    }

    public PendingTokenData? ValidateAccessToken(string token)
    {
        try
        {
            var decoded = JWT.Decode<Dictionary<string, object>>(token, GetKey());
            if (decoded["scope"]?.ToString() != ACCESS_TOKEN_SCOPE) return null;
            if (!int.TryParse(decoded["sub"]?.ToString(), out var id)) return null;
            return new PendingTokenData(id);
        }
        catch
        {
            return null;
        }
    }

    public PendingTokenData? ValidateRefreshToken(string token)
    {
        try
        {
            var decoded = JWT.Decode<Dictionary<string, object>>(token, GetKey());
            if (decoded["scope"]?.ToString() != REFRESH_TOKEN_SCOPE) return null;
            if (!int.TryParse(decoded["sub"]?.ToString(), out var id)) return null;
            return new PendingTokenData(id);
        }
        catch
        {
            return null;
        }
    }

    private byte[] GetKey() => Encoding.UTF8.GetBytes(_jwtSecret);
}

public record PendingTokenData(int PendingRegistrationId);
