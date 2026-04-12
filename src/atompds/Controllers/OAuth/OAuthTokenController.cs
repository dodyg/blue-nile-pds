using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using atompds.Services.OAuth;
using Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace atompds.Controllers.OAuth;

[ApiController]
public class OAuthTokenController : ControllerBase
{
    private readonly ILogger<OAuthTokenController> _logger;
    private readonly OAuthSessionStore _sessionStore;
    private readonly SecretsConfig _secretsConfig;
    private readonly ServiceConfig _serviceConfig;

    public OAuthTokenController(
        OAuthSessionStore sessionStore,
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig,
        ILogger<OAuthTokenController> logger)
    {
        _sessionStore = sessionStore;
        _serviceConfig = serviceConfig;
        _secretsConfig = secretsConfig;
        _logger = logger;
    }

    [HttpPost("oauth/token")]
    [EnableRateLimiting("auth-sensitive")]
    public IActionResult Token([FromForm] string? grant_type,
        [FromForm] string? code,
        [FromForm] string? redirect_uri,
        [FromForm] string? code_verifier,
        [FromForm] string? refresh_token,
        [FromForm] string? client_id)
    {
        switch (grant_type)
        {
            case "authorization_code":
                return HandleAuthorizationCode(code, code_verifier, client_id);
            case "refresh_token":
                return HandleRefreshToken(refresh_token);
            default:
                return BadRequest(new { error = "unsupported_grant_type", error_description = $"Grant type '{grant_type}' is not supported" });
        }
    }

    private IActionResult HandleAuthorizationCode(string? code, string? codeVerifier, string? clientId)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_request", error_description = "code is required" });
        if (string.IsNullOrWhiteSpace(codeVerifier))
            return BadRequest(new { error = "invalid_request", error_description = "code_verifier is required" });

        var oauthCode = _sessionStore.ExchangeCode(code, codeVerifier);
        if (oauthCode == null)
            return BadRequest(new { error = "invalid_grant", error_description = "Invalid or expired authorization code" });

        var (accessToken, refreshToken, expiresIn) = GenerateTokens(oauthCode.Did, oauthCode.Scope);

        return Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "DPoP",
            ExpiresIn = expiresIn,
            Scope = oauthCode.Scope,
            Sub = oauthCode.Did
        });
    }

    private IActionResult HandleRefreshToken(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });

        var principal = ValidateToken(refreshToken);
        if (principal == null)
            return BadRequest(new { error = "invalid_grant", error_description = "Invalid refresh token" });

        var did = principal.FindFirst("sub")?.Value;
        var scope = principal.FindFirst("scope")?.Value ?? "atproto";
        if (did == null)
            return BadRequest(new { error = "invalid_grant", error_description = "Invalid refresh token" });

        var (accessToken, newRefreshToken, expiresIn) = GenerateTokens(did, scope);

        return Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            TokenType = "DPoP",
            ExpiresIn = expiresIn,
            Scope = scope,
            Sub = did
        });
    }

    private (string accessToken, string refreshToken, long expiresIn) GenerateTokens(string did, string scope)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(15);
        var refreshExpiry = now.AddDays(90);

        var dpopJwk = HttpContext.Request.Headers["DPoP"].FirstOrDefault();
        var cnfClaim = dpopJwk != null ? ExtractJwkThumbprint(dpopJwk) : null;

        var accessClaims = new List<Claim>
        {
            new("sub", did),
            new("scope", scope),
            new("aud", _serviceConfig.Did),
            new("iat", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("exp", accessExpiry.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("jti", Guid.NewGuid().ToString())
        };
        if (cnfClaim != null)
        {
            accessClaims.Add(new Claim("cnf", $"{{\"jkt\":\"{cnfClaim}\"}}", JsonClaimValueTypes.Json));
        }

        var accessKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretsConfig.JwtSecret));
        var accessHandler = new JwtSecurityTokenHandler();
        var accessDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(accessClaims),
            Expires = accessExpiry.UtcDateTime,
            SigningCredentials = new SigningCredentials(accessKey, SecurityAlgorithms.HmacSha256),
            TokenType = "at+jwt"
        };
        var accessToken = accessHandler.CreateEncodedJwt(accessDescriptor);

        var refreshClaims = new List<Claim>
        {
            new("sub", did),
            new("scope", scope),
            new("aud", _serviceConfig.Did),
            new("iat", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("exp", refreshExpiry.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("jti", Guid.NewGuid().ToString())
        };
        var refreshKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretsConfig.JwtSecret));
        var refreshHandler = new JwtSecurityTokenHandler();
        var refreshDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(refreshClaims),
            Expires = refreshExpiry.UtcDateTime,
            SigningCredentials = new SigningCredentials(refreshKey, SecurityAlgorithms.HmacSha256),
            TokenType = "rt+jwt"
        };
        var refreshTokenStr = refreshHandler.CreateEncodedJwt(refreshDescriptor);

        return (accessToken, refreshTokenStr, 900);
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretsConfig.JwtSecret));
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = true,
                ValidAudience = _serviceConfig.Did,
                ValidateLifetime = true,
                IssuerSigningKey = key,
                ValidTypes = ["rt+jwt"]
            };
            var principal = handler.ValidateToken(token, parameters, out _);
            return principal;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to validate refresh token");
            return null;
        }
    }

    private string? ExtractJwkThumbprint(string dpopProof)
    {
        try
        {
            var parts = dpopProof.Split('.');
            if (parts.Length != 3) return null;
            var headerJson = Encoding.UTF8.GetString(Jose.Base64Url.Decode(parts[0]));
            var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerJson);
            if (header == null || !header.TryGetValue("jwk", out var jwk)) return null;
            return ComputeJwkThumbprint(jwk);
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeJwkThumbprint(JsonElement jwk)
    {
        var normalized = new SortedDictionary<string, string>();
        if (jwk.TryGetProperty("kty", out var kty)) normalized["kty"] = kty.GetString() ?? "";
        if (jwk.TryGetProperty("crv", out var crv)) normalized["crv"] = crv.GetString() ?? "";
        if (jwk.TryGetProperty("x", out var x)) normalized["x"] = x.GetString() ?? "";
        if (jwk.TryGetProperty("y", out var y)) normalized["y"] = y.GetString() ?? "";
        if (jwk.TryGetProperty("e", out var e)) normalized["e"] = e.GetString() ?? "";
        if (jwk.TryGetProperty("n", out var n)) normalized["n"] = n.GetString() ?? "";

        var canonicalJson = JsonSerializer.Serialize(normalized);
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Jose.Base64Url.Encode(hash);
    }
}

public record TokenResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string TokenType { get; set; } = "DPoP";
    public long ExpiresIn { get; set; }
    public string Scope { get; set; } = "";
    public string Sub { get; set; } = "";
}
