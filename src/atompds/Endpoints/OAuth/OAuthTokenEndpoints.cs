using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using atompds.Services;
using atompds.Services.OAuth;
using Config;
using Microsoft.AspNetCore.RateLimiting;

namespace atompds.Endpoints.OAuth;

public static class OAuthTokenEndpoints
{
    public static WebApplication MapOAuthTokenEndpoints(this WebApplication app)
    {
        app.MapPost("oauth/token", HandleAsync).RequireRateLimiting("auth-sensitive");
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        OAuthSessionStore sessionStore,
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig,
        EntrywayRelayService entrywayRelayService,
        ILogger<Program> logger)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (entrywayRelayService.IsConfigured)
        {
            return await entrywayRelayService.ForwardFormAsync(
                context.Request,
                "/oauth/token",
                form,
                context.RequestAborted);
        }

        var grantType = form["grant_type"].FirstOrDefault();
        var code = form["code"].FirstOrDefault();
        var codeVerifier = form["code_verifier"].FirstOrDefault();
        var refreshToken = form["refresh_token"].FirstOrDefault();
        var clientId = form["client_id"].FirstOrDefault();

        return grantType switch
        {
            "authorization_code" => HandleAuthorizationCode(code, codeVerifier, clientId, sessionStore, serviceConfig, secretsConfig, context, logger),
            "refresh_token" => HandleRefreshToken(refreshToken, sessionStore, serviceConfig, secretsConfig, context, logger),
            _ => Results.BadRequest(new { error = "unsupported_grant_type", error_description = $"Grant type '{grantType}' is not supported" })
        };
    }

    private static IResult HandleAuthorizationCode(string? code, string? codeVerifier, string? clientId, OAuthSessionStore sessionStore, ServiceConfig serviceConfig, SecretsConfig secretsConfig, HttpContext context, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Results.BadRequest(new { error = "invalid_request", error_description = "code is required" });
        if (string.IsNullOrWhiteSpace(codeVerifier))
            return Results.BadRequest(new { error = "invalid_request", error_description = "code_verifier is required" });

        var oauthCode = sessionStore.ExchangeCode(code, codeVerifier);
        if (oauthCode == null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "Invalid or expired authorization code" });

        var (accessToken, refreshToken, expiresIn) = GenerateTokens(oauthCode.Did, oauthCode.Scope, serviceConfig, secretsConfig, context, logger);

        return Results.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "DPoP",
            ExpiresIn = expiresIn,
            Scope = oauthCode.Scope,
            Sub = oauthCode.Did
        });
    }

    private static IResult HandleRefreshToken(string? refreshToken, OAuthSessionStore sessionStore, ServiceConfig serviceConfig, SecretsConfig secretsConfig, HttpContext context, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Results.BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });

        var principal = ValidateToken(refreshToken, serviceConfig, secretsConfig, logger);
        if (principal == null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "Invalid refresh token" });

        var did = principal.FindFirst("sub")?.Value;
        var scope = principal.FindFirst("scope")?.Value ?? "atproto";
        if (did == null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "Invalid refresh token" });

        var (accessToken, newRefreshToken, expiresIn) = GenerateTokens(did, scope, serviceConfig, secretsConfig, context, logger);

        return Results.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            TokenType = "DPoP",
            ExpiresIn = expiresIn,
            Scope = scope,
            Sub = did
        });
    }

    private static (string accessToken, string refreshToken, long expiresIn) GenerateTokens(string did, string scope, ServiceConfig serviceConfig, SecretsConfig secretsConfig, HttpContext context, ILogger logger)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(15);
        var refreshExpiry = now.AddDays(90);

        var dpopJwk = context.Request.Headers["DPoP"].FirstOrDefault();
        var cnfClaim = dpopJwk != null ? ExtractJwkThumbprint(dpopJwk, logger) : null;

        var accessClaims = new List<Claim>
        {
            new("sub", did),
            new("iss", serviceConfig.PublicUrl),
            new("scope", scope),
            new("aud", serviceConfig.Did),
            new("iat", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("exp", accessExpiry.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("jti", Guid.NewGuid().ToString())
        };
        if (cnfClaim != null)
        {
            accessClaims.Add(new Claim("cnf", $"{{\"jkt\":\"{cnfClaim}\"}}", JsonClaimValueTypes.Json));
        }

        var accessKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretsConfig.JwtSecret));
        var accessHandler = new JwtSecurityTokenHandler();
        var accessDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(accessClaims),
            Expires = accessExpiry.UtcDateTime,
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(accessKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256),
            TokenType = "at+jwt"
        };
        var accessToken = accessHandler.CreateEncodedJwt(accessDescriptor);

        var refreshClaims = new List<Claim>
        {
            new("sub", did),
            new("iss", serviceConfig.PublicUrl),
            new("scope", scope),
            new("aud", serviceConfig.Did),
            new("iat", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("exp", refreshExpiry.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("jti", Guid.NewGuid().ToString())
        };
        var refreshKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretsConfig.JwtSecret));
        var refreshHandler = new JwtSecurityTokenHandler();
        var refreshDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(refreshClaims),
            Expires = refreshExpiry.UtcDateTime,
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(refreshKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256),
            TokenType = "rt+jwt"
        };
        var refreshTokenStr = refreshHandler.CreateEncodedJwt(refreshDescriptor);

        return (accessToken, refreshTokenStr, 900);
    }

    private static ClaimsPrincipal? ValidateToken(string token, ServiceConfig serviceConfig, SecretsConfig secretsConfig, ILogger logger)
    {
        try
        {
            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretsConfig.JwtSecret));
            var handler = new JwtSecurityTokenHandler();
            var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = serviceConfig.PublicUrl,
                ValidateAudience = true,
                ValidAudience = serviceConfig.Did,
                ValidateLifetime = true,
                IssuerSigningKey = key,
                ValidTypes = ["rt+jwt"]
            };
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to validate refresh token");
            return null;
        }
    }

    private static string? ExtractJwkThumbprint(string dpopProof, ILogger logger)
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
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to extract JWK thumbprint from DPoP proof");
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
