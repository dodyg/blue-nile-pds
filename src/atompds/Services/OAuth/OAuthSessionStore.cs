using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace atompds.Services.OAuth;

public class OAuthSessionStore
{
    private readonly ConcurrentDictionary<string, OAuthAuthorization> _authorizations = new();
    private readonly ConcurrentDictionary<string, OAuthCode> _codes = new();

    public OAuthAuthorization CreateAuthorization(string clientId, string redirectUri, string scope, string state,
        string codeChallenge, string codeChallengeMethod, string? loginHint = null)
    {
        var auth = new OAuthAuthorization
        {
            Id = RandomHex(16),
            ClientId = clientId,
            RedirectUri = redirectUri,
            Scope = scope,
            State = state,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            LoginHint = loginHint,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _authorizations[auth.Id] = auth;
        return auth;
    }

    public OAuthAuthorization? GetAuthorization(string id)
    {
        if (!_authorizations.TryGetValue(id, out var auth)) return null;
        if (auth.ExpiresAt < DateTime.UtcNow)
        {
            _authorizations.TryRemove(id, out _);
            return null;
        }

        return auth;
    }

    public OAuthCode IssueCode(string authorizationId, string did, string scope)
    {
        CleanupExpired();

        var code = new OAuthCode
        {
            Code = RandomHex(32),
            AuthorizationId = authorizationId,
            Did = did,
            Scope = scope,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(1),
            Used = false
        };

        _codes[code.Code] = code;
        return code;
    }

    public OAuthCode? ExchangeCode(string code, string codeVerifier)
    {
        if (!_codes.TryGetValue(code, out var oauthCode)) return null;
        if (oauthCode.Used || oauthCode.ExpiresAt < DateTime.UtcNow) return null;

        var auth = GetAuthorization(oauthCode.AuthorizationId);
        if (auth == null) return null;

        var expectedChallenge = ComputeS256Challenge(codeVerifier);
        if (expectedChallenge != auth.CodeChallenge) return null;

        oauthCode.Used = true;
        return oauthCode;
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _authorizations)
        {
            if (kvp.Value.ExpiresAt < now)
                _authorizations.TryRemove(kvp.Key, out _);
        }

        foreach (var kvp in _codes)
        {
            if (kvp.Value.ExpiresAt < now)
                _codes.TryRemove(kvp.Key, out _);
        }
    }

    private static string RandomHex(int bytes)
    {
        var buf = new byte[bytes];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToHexString(buf).ToLowerInvariant();
    }

    private static string ComputeS256Challenge(string verifier)
    {
        var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

public class OAuthAuthorization
{
    public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string Scope { get; set; } = "";
    public string State { get; set; } = "";
    public string CodeChallenge { get; set; } = "";
    public string CodeChallengeMethod { get; set; } = "S256";
    public string? LoginHint { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class OAuthCode
{
    public string Code { get; set; } = "";
    public string AuthorizationId { get; set; } = "";
    public string Did { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
}
