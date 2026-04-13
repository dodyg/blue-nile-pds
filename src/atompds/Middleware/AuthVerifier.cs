using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using Crypto;
using Identity;
using Jose;
using Xrpc;

namespace atompds.Middleware;

public record AuthVerifierConfig(
    string JwtKey,
    string AdminPass,
    string PublicUrl,
    string PdsDid,
    string? EntrywayUrl,
    string? EntrywayDid,
    string? EntrywayJwtVerifyKeyK256PublicKeyHex);

public class AuthVerifier
{

    public enum AuthScope
    {
        Access,
        Refresh,
        AppPass,
        AppPassPrivileged,
        SignupQueued
    }

    public static readonly IReadOnlyDictionary<AuthScope, string> ScopeMap = new Dictionary<AuthScope, string>
    {
        {AuthScope.Access, Auth.ACCESS_TOKEN_SCOPE},
        {AuthScope.Refresh, Auth.REFRESH_TOKEN_SCOPE},
        {AuthScope.AppPass, "com.atproto.appPass"},
        {AuthScope.AppPassPrivileged, "com.atproto.appPassPrivileged"},
        {AuthScope.SignupQueued, "com.atproto.signupQueued"}
    };

    private readonly AccountRepository _accountRepository;
    private readonly AuthVerifierConfig _config;
    private readonly IdResolver _idResolver;
    private readonly string? _entrywayJwtDidKey;
    public AuthVerifier(AccountRepository accountRepository, IdResolver idResolver, AuthVerifierConfig config)
    {
        _accountRepository = accountRepository;
        _idResolver = idResolver;
        _config = config;
        _entrywayJwtDidKey = string.IsNullOrWhiteSpace(config.EntrywayJwtVerifyKeyK256PublicKeyHex)
            ? null
            : Did.FormatDidKey(Const.SECP256K1_JWT_ALG, Convert.FromHexString(config.EntrywayJwtVerifyKeyK256PublicKeyHex));
    }

    public async Task<AccessOutput> AccessStandardAsync(HttpContext ctx, bool checkTakenDown = false, bool checkDeactivated = false)
    {
        return await ValidateAccessTokenAsync(ctx,
        [
            ScopeMap[AuthScope.Access],
            ScopeMap[AuthScope.AppPass],
            ScopeMap[AuthScope.AppPassPrivileged]
        ], checkTakenDown, checkDeactivated);
    }

    public async Task<AccessOutput> AccessFullAsync(HttpContext ctx, bool checkTakenDown = false, bool checkDeactivated = false)
    {
        return await ValidateAccessTokenAsync(ctx,
        [
            ScopeMap[AuthScope.Access]
        ], checkTakenDown, checkDeactivated);
    }

    public async Task<AccessOutput> AccessPrivilegedAsync(HttpContext ctx, bool checkTakenDown = false, bool checkDeactivated = false)
    {
        return await ValidateAccessTokenAsync(ctx,
        [
            ScopeMap[AuthScope.Access],
            ScopeMap[AuthScope.AppPassPrivileged]
        ], checkTakenDown, checkDeactivated);
    }

    public RefreshOutput Refresh(HttpContext ctx)
    {
        var parsedHeader = ParseAuthorizationHeader(ctx.Request.Headers.Authorization);
        var result = ValidateBearerToken(parsedHeader, [
            ScopeMap[AuthScope.Refresh]
        ], new VerifyOptions
        {
            Audience = _config.PdsDid,
            Type = "rt+jwt"
        }, allowCnf: false);

        var decoded = JsonSerializer.Deserialize<Dictionary<string, object>>(result.Payload) ??
                      throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));

        if (!decoded.TryGetValue("jti", out var value) || string.IsNullOrWhiteSpace(value.ToString()))
        {
            throw new XRPCError(ResponseType.AuthRequired, new ErrorDetail("MissingTokenId", "Unexpected missing refresh token id"));
        }

        return new RefreshOutput(new RefreshCredentials
        {
            Did = result.Did,
            Scope = result.Scope,
            Audience = result.Audience,
            IsPrivileged = false,
            TokenId = value.ToString()!
        }, result.Token);
    }

    public async Task<AccessOutput> ValidateAccessTokenAsync(HttpContext ctx, string[] scopes, bool checkTakenDown = false, bool checkDeactivated = false)
    {
        if (ctx.Response.HasStarted)
        {
            throw new Exception("Response has already started");
        }

        ctx.Response.OnStarting(() =>
        {
            SetAuthHeaders(ctx);
            return Task.CompletedTask;
        });

        var (type, token) = ParseAuthorizationHeader(ctx.Request.Headers.Authorization);
        AccessOutput accessOutput;
        switch (type)
        {
            case AuthType.BEARER:
                accessOutput = ValidateBearerAccessToken(ctx, scopes);
                break;
            case AuthType.DPOP:
                accessOutput = ValidateDpopAccessToken(ctx, scopes);
                break;
            default:
                throw new XRPCError(new InvalidTokenErrorDetail("Unexpected authorization type"));
        }

        if (checkTakenDown || checkDeactivated)
        {
            var found = await _accountRepository.GetAccountAsync(accessOutput.AccessCredentials.Did, new AvailabilityFlags(checkTakenDown, checkDeactivated));
            if (found == null)
            {
                throw new XRPCError(ResponseType.Forbidden, new ErrorDetail("AccountNotFound", "Account not found"));
            }

            if (checkTakenDown && found.SoftDeleted)
            {
                throw new XRPCError(new ErrorDetail("AccountTakenDown", "Account has been taken down"));
            }

            if (checkDeactivated && found.DeactivatedAt != null)
            {
                throw new XRPCError(new ErrorDetail("AccountDeactivated", "Account has been deactivated"));
            }
        }

        return accessOutput;
    }

    private AccessOutput ValidateBearerAccessToken(HttpContext ctx, string[] scopes)
    {
        var auth = ParseAuthorizationHeader(ctx.Request.Headers.Authorization);
        var validated = ValidateBearerToken(auth, scopes, new VerifyOptions
        {
            Audience = _config.PdsDid,
            Type = "at+jwt"
        }, allowCnf: false);

        return new AccessOutput(new AccessCredentials
        {
            Did = validated.Did,
            Scope = validated.Scope,
            Audience = validated.Audience,
            IsPrivileged = false
        }, validated.Token);
    }

    private AccessOutput ValidateDpopAccessToken(HttpContext ctx, string[] scopes)
    {
        var auth = ParseAuthorizationHeader(ctx.Request.Headers.Authorization);
        if (IsOAuthToken(auth.Token))
        {
            var validated = ValidateOAuthToken(auth.Token);

            var dpopProof = ctx.Request.Headers["DPoP"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dpopProof))
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Missing DPoP proof header"));
            }

            var requestUrl = $"{_config.PublicUrl}{ctx.Request.Path}{ctx.Request.QueryString}";
            ValidateDpopProof(dpopProof, validated.Payload, ctx.Request.Method, requestUrl);

            return new AccessOutput(new AccessCredentials
            {
                Did = validated.Did,
                Scope = validated.Scope,
                Audience = validated.Audience,
                IsPrivileged = false
            }, validated.Token);
        }

        var legacyValidated = ValidateBearerToken(auth, scopes, new VerifyOptions
        {
            Audience = _config.PdsDid,
            Type = "at+jwt"
        }, allowCnf: true);

        var legacyDpopProof = ctx.Request.Headers["DPoP"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(legacyDpopProof))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Missing DPoP proof header"));
        }

        var requestPath = $"{_config.PublicUrl}{ctx.Request.Path}{ctx.Request.QueryString}";
        ValidateDpopProof(legacyDpopProof, legacyValidated.Payload, ctx.Request.Method, requestPath);

        return new AccessOutput(new AccessCredentials
        {
            Did = legacyValidated.Did,
            Scope = legacyValidated.Scope,
            Audience = legacyValidated.Audience,
            IsPrivileged = false
        }, legacyValidated.Token);
    }

    private void ValidateDpopProof(string dpopProof, string accessTokenPayload, string method, string requestUrl)
    {
        string[] parts;
        try
        {
            parts = dpopProof.Split('.');
            if (parts.Length != 3)
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Invalid DPoP proof format"));
            }
        }
        catch (XRPCError)
        {
            throw;
        }
        catch
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Invalid DPoP proof format"));
        }

        Dictionary<string, JsonElement> headerJson;
        Dictionary<string, JsonElement> payloadJson;
        try
        {
            var headerBytes = Base64Url.Decode(parts[0]);
            var payloadBytes = Base64Url.Decode(parts[1]);
            headerJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerBytes) ?? throw new Exception();
            payloadJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadBytes) ?? throw new Exception();
        }
        catch (XRPCError)
        {
            throw;
        }
        catch
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Invalid DPoP proof encoding"));
        }

        if (!headerJson.TryGetValue("typ", out var typ) || typ.GetString() != "dpop+jwt")
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Invalid DPoP proof type"));
        }

        if (!headerJson.TryGetValue("jwk", out var jwkElement))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Missing jwk in DPoP proof"));
        }

        VerifyDpopProofSignature(parts, headerJson, jwkElement);

        var jwkThumbprint = ComputeJwkThumbprint(jwkElement);

        var tokenData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(accessTokenPayload);
        if (tokenData != null && tokenData.TryGetValue("cnf", out var cnfElement))
        {
            var cnfObj = cnfElement.Deserialize<Dictionary<string, string>>();
            if (cnfObj != null && cnfObj.TryGetValue("jkt", out var expectedJkt))
            {
                if (expectedJkt != jwkThumbprint)
                {
                    throw new XRPCError(new InvalidTokenErrorDetail("DPoP proof key does not match token binding"));
                }
            }
        }

        if (!payloadJson.TryGetValue("htm", out var htm) || htm.GetString() != method)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("DPoP proof method mismatch"));
        }

        if (!payloadJson.TryGetValue("htu", out var htu) ||
            !string.Equals(htu.GetString(), requestUrl, StringComparison.Ordinal))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("DPoP proof URI mismatch"));
        }

        if (!payloadJson.TryGetValue("iat", out var iat) ||
            iat.ValueKind != JsonValueKind.Number ||
            !iat.TryGetInt64(out var issuedAt) ||
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issuedAt) > 300)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Invalid DPoP proof freshness"));
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
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Base64Url.Encode(hash);
    }

    private static void VerifyDpopProofSignature(string[] parts, Dictionary<string, JsonElement> headerJson, JsonElement jwkElement)
    {
        if (!headerJson.TryGetValue("alg", out var alg) || alg.GetString() != Const.P256_JWT_ALG)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Unsupported DPoP proof algorithm"));
        }

        if (!jwkElement.TryGetProperty("kty", out var kty) || kty.GetString() != "EC" ||
            !jwkElement.TryGetProperty("crv", out var crv) || crv.GetString() != "P-256" ||
            !jwkElement.TryGetProperty("x", out var x) || !jwkElement.TryGetProperty("y", out var y))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Unsupported DPoP proof key"));
        }

        try
        {
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = Base64Url.Decode(x.GetString()!),
                    Y = Base64Url.Decode(y.GetString()!)
                }
            };

            using var ecdsa = ECDsa.Create(parameters);
            var signingInput = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64Url.Decode(parts[2]);
            var valid = ecdsa.VerifyData(
                signingInput,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            if (!valid)
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Invalid DPoP proof signature"));
            }
        }
        catch (XRPCError)
        {
            throw;
        }
        catch
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Invalid DPoP proof signature"));
        }
    }

    private bool IsOAuthToken(string token)
    {
        var payload = DecodeJwtSection(token, 1);
        if (payload == null)
        {
            return false;
        }

        if (payload.TryGetValue("scope", out var scopeElement) &&
            scopeElement.ValueKind == JsonValueKind.String &&
            scopeElement.GetString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains("atproto", StringComparer.Ordinal) == true)
        {
            return true;
        }

        return payload.TryGetValue("iss", out var issElement) &&
               issElement.ValueKind == JsonValueKind.String &&
               string.Equals(issElement.GetString(), _config.EntrywayUrl, StringComparison.Ordinal);
    }

    private ValidatedBearer ValidateOAuthToken(string token)
    {
        var payload = VerifyOAuthJwt(token);
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload)
                   ?? throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));

        if (!data.TryGetValue("sub", out var didElement) ||
            didElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(didElement.GetString()) ||
            !didElement.GetString()!.StartsWith("did:", StringComparison.Ordinal))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed token"));
        }

        if (!data.TryGetValue("scope", out var scopeElement) ||
            scopeElement.ValueKind != JsonValueKind.String)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed token"));
        }

        var scope = scopeElement.GetString()!;
        var scopeTokens = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!scopeTokens.Contains("atproto", StringComparer.Ordinal))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Bad token scope"));
        }

        if (!data.TryGetValue("aud", out var audienceElement) ||
            audienceElement.ValueKind != JsonValueKind.String ||
            audienceElement.GetString() != _config.PdsDid)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Invalid token audience"));
        }

        if (!data.TryGetValue("cnf", out var cnfElement) ||
            cnfElement.ValueKind != JsonValueKind.Object ||
            !cnfElement.TryGetProperty("jkt", out var jktElement) ||
            jktElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(jktElement.GetString()))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed token"));
        }

        return new ValidatedBearer(didElement.GetString()!, scope, token, payload, audienceElement.GetString());
    }

    private string VerifyOAuthJwt(string token)
    {
        var payload = DecodeJwtSection(token, 1)
                      ?? throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));

        if (payload.TryGetValue("iss", out var issElement) &&
            issElement.ValueKind == JsonValueKind.String &&
            string.Equals(issElement.GetString(), _config.EntrywayUrl, StringComparison.Ordinal))
        {
            return VerifyEntrywayOAuthJwt(token);
        }

        return JwtVerify(token, new VerifyOptions
        {
            Audience = _config.PdsDid,
            Type = "at+jwt"
        }).payload;
    }

    private string VerifyEntrywayOAuthJwt(string token)
    {
        if (string.IsNullOrWhiteSpace(_config.EntrywayUrl) || string.IsNullOrWhiteSpace(_entrywayJwtDidKey))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
        }

        try
        {
            var headerJson = Encoding.UTF8.GetString(Base64Url.Decode(parts[0]));
            var payloadJson = Encoding.UTF8.GetString(Base64Url.Decode(parts[1]));
            var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerJson)
                         ?? throw new Exception();
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson)
                          ?? throw new Exception();

            if (header.TryGetValue("typ", out var typElement) &&
                typElement.ValueKind == JsonValueKind.String &&
                typElement.GetString() != "at+jwt")
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Invalid token type"));
            }

            if (!header.TryGetValue("alg", out var algElement) ||
                algElement.ValueKind != JsonValueKind.String ||
                algElement.GetString() != Const.SECP256K1_JWT_ALG)
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
            }

            if (!payload.TryGetValue("iss", out var issElement) ||
                issElement.ValueKind != JsonValueKind.String ||
                !string.Equals(issElement.GetString(), _config.EntrywayUrl, StringComparison.Ordinal))
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
            }

            if (!payload.TryGetValue("aud", out var audienceElement) ||
                audienceElement.ValueKind != JsonValueKind.String ||
                audienceElement.GetString() != _config.PdsDid)
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Invalid token audience"));
            }

            if (!payload.TryGetValue("exp", out var expElement) ||
                expElement.ValueKind != JsonValueKind.Number ||
                !expElement.TryGetInt64(out var exp) ||
                exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
            }

            var signingInput = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            var sig = Base64Url.Decode(parts[2]);
            if (!Verify.VerifySignature(_entrywayJwtDidKey, signingInput, sig, null, algElement.GetString()))
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
            }

            return payloadJson;
        }
        catch (XRPCError)
        {
            throw;
        }
        catch
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
        }
    }

    private static Dictionary<string, JsonElement>? DecodeJwtSection(string token, int partIndex)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64Url.Decode(parts[partIndex]));
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch
        {
            return null;
        }
    }

    private ValidatedBearer ValidateBearerToken(ParsedAuthHeader auth, string[] scopes, VerifyOptions options, bool allowCnf = false)
    {
        if (auth.Type != AuthType.BEARER && auth.Type != AuthType.DPOP)
        {
            throw new Exception("Invalid auth type");
        }

        if (string.IsNullOrWhiteSpace(auth.Token))
        {
            throw new XRPCError(new ErrorDetail("AuthMissing", ""));
        }

        var (payload, headers) = JwtVerify(auth.Token, options);

        if (headers["typ"].ToString() != options.Type)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Invalid token type"));
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(payload) ?? throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
        var did = data["sub"].ToString();
        var scope = data["scope"].ToString();
        var audience = data.TryGetValue("aud", out var aud) ? aud.ToString() : null;
        if (string.IsNullOrWhiteSpace(did) || !did.StartsWith("did:"))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed token"));
        }

        if (audience != null && !audience.StartsWith("did:"))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed token"));
        }

        if (data.ContainsKey("cnf") && !allowCnf)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed token"));
        }

        if (string.IsNullOrWhiteSpace(scope) || !IsAuthScope(scope) || scopes.Length > 0 && !scopes.Contains(scope))
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Bad token scope"));
        }

        return new ValidatedBearer(did, scope, auth.Token, payload, audience);
    }

    private static bool IsAuthScope(string scope)
    {
        return ScopeMap.Values.Contains(scope);
    }

    private (string payload, IDictionary<string, object> protectedHeader) JwtVerify(string token, VerifyOptions options)
    {
        try
        {
            var json = JWT.Verify(token, Encoding.UTF8.GetBytes(_config.JwtKey));
            var headers = JWT.Headers(token);
            var type = headers["typ"]?.ToString();
            if (type != options.Type)
            {
                throw new XRPCError(new InvalidTokenErrorDetail("Invalid token type"));
            }

            if (options.Audience != null)
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ??
                           throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
                var aud = data["aud"].ToString();
                if (aud != options.Audience)
                {
                    throw new XRPCError(new InvalidTokenErrorDetail("Invalid token audience"));
                }
            }

            return (json, headers);
        }
        catch (Exception e)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Token could not be verified"));
        }
    }

    private ParsedAuthHeader ParseAuthorizationHeader(string? authorization)
    {
        if (authorization == null)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Missing authorization header"));
        }

        var result = authorization.Split(' ');
        if (result.Length != 2)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed authorization header"));
        }

        var authType = result[0].ToUpper();
        if (!Enum.TryParse<AuthType>(authType, out var type))
        {
            throw new XRPCError(new InvalidTokenErrorDetail($"Unsupported authorization type: {authType}"));
        }

        return new ParsedAuthHeader(type, result[1]);
    }
    private ParsedBasicAuth? ParseBasicAuthorization(string? authorization)
    {
        if (authorization == null)
        {
            return null;
        }

        var result = authorization.Split(' ');
        if (result.Length != 2)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed authorization header"));
        }

        var authType = result[0].ToUpper();
        if (authType != "BASIC")
        {
            throw new XRPCError(new InvalidTokenErrorDetail($"Unsupported authorization type: {authType}"));
        }

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(result[1]));
        var parts = decoded.Split(':');
        if (parts.Length != 2)
        {
            throw new XRPCError(new InvalidTokenErrorDetail("Malformed basic auth header"));
        }

        return new ParsedBasicAuth(parts[0], parts[1]);
    }

    private void SetAuthHeaders(HttpContext ctx)
    {
        var res = ctx.Response;
        res.Headers.CacheControl = "private";
        var currentVary = res.Headers.Vary;
        const string authorization = "Authorization";
        if (currentVary.Count == 0)
        {
            res.Headers.Vary = authorization;
        }
        else
        {
            var alreadyIncluded = currentVary.Contains(authorization);
            if (!alreadyIncluded)
            {
                res.Headers.Append("Vary", authorization);
            }
        }
    }
    public Task<AdminOutput> AdminTokenAsync(HttpContext context)
    {
        var auth = ParseBasicAuthorization(context.Request.Headers.Authorization);
        if (auth == null || auth.Username != "admin" || auth.Password != _config.AdminPass)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Invalid admin credentials"));
        }

        return Task.FromResult(new AdminOutput(new AdminCredentials(), ""));
    }

    public abstract record AuthOutput
    {
        public AuthOutput(IAuthCredentials credentials, string artifacts)
        {
            Credentials = credentials;
            Artifacts = artifacts;
        }

        public IAuthCredentials Credentials { get; }
        public string Artifacts { get; }
    }

    public interface IAuthCredentials
    {
        public string Type { get; }
    }

    public record AccessOutput(AccessCredentials AccessCredentials, string Artifacts) : AuthOutput(AccessCredentials, Artifacts);
    public record RefreshOutput(RefreshCredentials RefreshCredentials, string Artifacts) : AuthOutput(RefreshCredentials, Artifacts);
    public record AdminOutput(AdminCredentials AdminCredentials, string Artifacts) : AuthOutput(AdminCredentials, Artifacts);

    public record AdminCredentials : IAuthCredentials
    {
        public string Type => "admin_token";
    }

    public record AccessCredentials : IAuthCredentials
    {
        public required string Did { get; init; }
        public required string Scope { get; init; }
        public required string? Audience { get; init; }
        public required bool IsPrivileged { get; init; }
        public string Type => "access";
    }

    public record RefreshCredentials : IAuthCredentials
    {
        public required string Did { get; init; }
        public required string Scope { get; init; }
        public required string? Audience { get; init; }
        public required bool IsPrivileged { get; init; }
        public required string TokenId { get; init; }
        public string Type => "refresh";
    }

    public record ValidatedBearer(string Did, string Scope, string Token, string Payload, string? Audience);

    private record VerifyOptions
    {
        public string? Audience { get; init; }
        public string? Type { get; init; }
    }

    private enum AuthType
    {
        BASIC,
        BEARER,
        DPOP
    }

    private record ParsedAuthHeader(AuthType Type, string Token);

    private record ParsedBasicAuth(string Username, string Password);
}
