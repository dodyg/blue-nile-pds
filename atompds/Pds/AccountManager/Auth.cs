using System.Security.Cryptography;
using System.Text;
using atompds.Pds.AccountManager.Db;
using Jose;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace atompds.AccountManager;

public record AuthToken(string Scope, string Sub, long Exp);
public record RefreshToken(string Sub, long Exp, string Jti) : AuthToken(Auth.REFRESH_TOKEN_SCOPE, Sub, Exp);
public record AccessToken(string Sub, long Exp, string Jti) : AuthToken(Auth.ACCESS_TOKEN_SCOPE, Sub, Exp);

public class Auth
{
    private readonly AccountManagerDb _accountDb;
    public const string ACCESS_TOKEN_SCOPE = "com.atproto.access";
    public const string REFRESH_TOKEN_SCOPE = "com.atproto.refresh";
    
    public Auth(AccountManagerDb accountDb)
    {
        _accountDb = accountDb;
    }

    public (string AccessToken, string RefreshToken) CreateTokens(string did, string jwtKey, string serviceDid, string? scope = null, string? jti = null, TimeSpan? expiresIn = null)
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
        
        var payload = new Dictionary<string, object>()
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

        var encoded = JWT.Encode(payload, GetKey(jwtKey), JwsAlgorithm.HS256, extraHeaders: headers);
        return encoded;
    }
    
    public RefreshToken DecodeRefreshToken(string token, string jwtKey)
    {
        var decoded = JWT.Decode<Dictionary<string, object>>(token, GetKey(jwtKey));
        if (decoded["scope"].ToString() != REFRESH_TOKEN_SCOPE)
        {
            throw new Exception("Invalid refresh token scope");
        }
        
        var sub = decoded["sub"].ToString() ?? throw new Exception("Missing sub");
        var exp = long.Parse(decoded["exp"].ToString() ?? throw new Exception("Missing exp"));
        var jti = decoded["jti"].ToString() ?? throw new Exception("Missing jti");
        return new RefreshToken(sub, exp, jti);
    }

    public string CreateRefreshToken(string did, string jwtKey, string serviceDid, string? jti, TimeSpan? expiresIn)
    {
        var now = DateTimeOffset.UtcNow;
        jti ??= GetRefreshTokenId();

        var payload = new Dictionary<string, object>()
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

        var encoded = JWT.Encode(payload, GetKey(jwtKey), JwsAlgorithm.HS256, extraHeaders: headers);
        return encoded;
    }
    
    public async Task<bool> StoreRefreshToken(RefreshToken token, string? appPassword)
    {
        var match = await _accountDb.RefreshTokens.FirstOrDefaultAsync(t => t.Id == token.Jti);
        if (match != null)
        {
            return false;
        }
        
        await _accountDb.RefreshTokens.AddAsync(new Pds.AccountManager.Db.Schema.RefreshToken
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