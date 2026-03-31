using System.Security.Cryptography;
using System.Text;
using AccountManager.Db;
using Jose;
using Microsoft.EntityFrameworkCore;
using RefreshToken = AccountManager.Db.RefreshToken;

namespace AccountManager;

public class Auth
{
    public const string ACCESS_TOKEN_SCOPE = "com.atproto.access";
    public const string REFRESH_TOKEN_SCOPE = "com.atproto.refresh";
    private readonly AccountManagerDb _accountDb;

    public Auth(AccountManagerDb accountDb)
    {
        _accountDb = accountDb;
    }

    public (string AccessToken, string RefreshToken) CreateTokens(string did,
        string jwtKey,
        string serviceDid,
        string? scope = null,
        string? jti = null,
        TimeSpan? expiresIn = null)
    {
        var accessToken = CreateAccessToken(did, jwtKey, serviceDid, scope, expiresIn);
        var refreshToken = CreateRefreshToken(did, jwtKey, serviceDid, jti, expiresIn);
        return (accessToken, refreshToken);
    }

    private byte[] GetKey(string jwtKey)
    {
        return Encoding.UTF8.GetBytes(jwtKey);
    }

    public string CreateAccessToken(string did, string jwtKey, string serviceDid, string? scope, TimeSpan? expiresIn)
    {
        var now = DateTimeOffset.UtcNow;

        var payload = new Dictionary<string, object>
        {
            ["scope"] = scope ?? ACCESS_TOKEN_SCOPE,
            ["sub"] = did,
            ["aud"] = serviceDid,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(expiresIn ?? TimeSpan.FromHours(2)).ToUnixTimeSeconds()
        };

        var headers = new Dictionary<string, object>
        {
            ["typ"] = "at+jwt"
        };

        var encoded = JWT.Encode(payload, GetKey(jwtKey), JwsAlgorithm.HS256, headers);
        return encoded;
    }

    /// <summary>
    ///     Unsafe for verification, should only be used w/ direct output from CreateRefreshToken
    /// </summary>
    public Types.RefreshToken DecodeRefreshTokenUnsafe(string token, string jwtKey)
    {
        var decoded = JWT.Decode<Dictionary<string, object>>(token, GetKey(jwtKey));
        if (decoded["scope"].ToString() != REFRESH_TOKEN_SCOPE)
        {
            throw new Exception("Invalid refresh token scope");
        }

        var sub = decoded["sub"].ToString() ?? throw new Exception("Missing sub");
        var exp = long.Parse(decoded["exp"].ToString() ?? throw new Exception("Missing exp"));
        var jti = decoded["jti"].ToString() ?? throw new Exception("Missing jti");
        return new Types.RefreshToken(sub, exp, jti);
    }

    public string CreateRefreshToken(string did, string jwtKey, string serviceDid, string? jti, TimeSpan? expiresIn)
    {
        var now = DateTimeOffset.UtcNow;
        jti ??= GetRefreshTokenId();

        var payload = new Dictionary<string, object>
        {
            ["scope"] = REFRESH_TOKEN_SCOPE,
            ["sub"] = did,
            ["aud"] = serviceDid,
            ["jti"] = jti,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(expiresIn ?? TimeSpan.FromDays(90)).ToUnixTimeSeconds()
        };

        var headers = new Dictionary<string, object>
        {
            ["typ"] = "refresh+jwt"
        };

        var encoded = JWT.Encode(payload, GetKey(jwtKey), JwsAlgorithm.HS256, headers);
        return encoded;
    }

    public async Task<bool> StoreRefreshToken(Types.RefreshToken token, string? appPassword)
    {
        var match = await _accountDb.RefreshTokens.FirstOrDefaultAsync(t => t.Id == token.Jti);
        if (match != null)
        {
            return false;
        }

        await _accountDb.RefreshTokens.AddAsync(new RefreshToken
        {
            Id = token.Jti,
            Did = token.Sub,
            AppPasswordName = appPassword,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(token.Exp).UtcDateTime
        });

        await _accountDb.SaveChangesAsync();
        return true;
    }

    public async Task DeleteExpiredRefreshTokens(string did, DateTimeOffset now)
    {
        var nowUtc = now.UtcDateTime;
        var expired = await _accountDb.RefreshTokens.Where(t => t.Did == did && t.ExpiresAt <= nowUtc).ToListAsync();
        _accountDb.RefreshTokens.RemoveRange(expired);
        await _accountDb.SaveChangesAsync();
    }

    public async Task AddRefreshGracePeriod(string jti, DateTimeOffset expiresAt, string nextId)
    {
        var tokenMatch = await _accountDb.RefreshTokens.Where(x => x.Id == jti && (x.NextId == null || x.NextId == nextId))
            .SingleOrDefaultAsync();
        if (tokenMatch != null)
        {
            tokenMatch.ExpiresAt = expiresAt.UtcDateTime;
            tokenMatch.NextId = nextId;
            var res = await _accountDb.SaveChangesAsync();
            if (res == 0)
            {
                throw new Exception("Failed to update refresh token");
            }
        }
    }

    public async Task<RefreshToken?> GetRefreshToken(string id)
    {
        return await _accountDb.RefreshTokens.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<(string AccessJwt, string RefreshJwt)?> RotateRefreshToken(string id, string jwtKey, string serviceDid)
    {
        var token = await GetRefreshToken(id);
        if (token == null) return null;

        var now = DateTime.UtcNow;

        // take the chance to tidy all of a user's expired tokens
        // does not need to be transactional since this is just best-effort
        await DeleteExpiredRefreshTokens(token.Did, new DateTimeOffset(now));

        // Shorten the refresh token lifespan down from its
        // original expiration time to its revocation grace period.
        var prevExpiresAt = token.ExpiresAt;
        var refreshGrace = TimeSpan.FromHours(2);
        var graceExpiresAt = now.Add(refreshGrace);

        var expiresAt = graceExpiresAt < prevExpiresAt ? graceExpiresAt : prevExpiresAt;

        if (expiresAt <= now)
        {
            return null;
        }

        // Determine the next refresh token id: upon refresh token
        // reuse you always receive a refresh token with the same id.
        var nextId = token.NextId ?? GetRefreshTokenId();

        var tokens = CreateTokens(token.Did, jwtKey, serviceDid, ACCESS_TOKEN_SCOPE, nextId);
        var refreshPayload = DecodeRefreshTokenUnsafe(tokens.RefreshToken, jwtKey);

        try
        {
            await AddRefreshGracePeriod(id, new DateTimeOffset(expiresAt), nextId);
            var stored = await StoreRefreshToken(refreshPayload, token.AppPasswordName);
            if (!stored)
            {
                // Concurrent refresh — retry
                return await RotateRefreshToken(id, jwtKey, serviceDid);
            }
        }
        catch
        {
            // Concurrent refresh — retry
            return await RotateRefreshToken(id, jwtKey, serviceDid);
        }

        return (tokens.AccessToken, tokens.RefreshToken);
    }

    public async Task<bool> RevokeRefreshToken(string jti)
    {
        _accountDb.RefreshTokens.RemoveRange(_accountDb.RefreshTokens.Where(t => t.Id == jti));
        return await _accountDb.SaveChangesAsync() > 0;
    }

    public async Task<bool> RevokeRefreshTokensByDid(string did)
    {
        _accountDb.RefreshTokens.RemoveRange(_accountDb.RefreshTokens.Where(t => t.Did == did));
        return await _accountDb.SaveChangesAsync() > 0;
    }

    public async Task<bool> RevokeAppPasswordRefreshToken(string did, string appPassword)
    {
        _accountDb.RefreshTokens.RemoveRange(_accountDb.RefreshTokens.Where(t => t.Did == did && t.AppPasswordName == appPassword));
        return await _accountDb.SaveChangesAsync() > 0;
    }


    private static string GetRefreshTokenId()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}