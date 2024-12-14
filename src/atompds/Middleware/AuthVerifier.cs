using System.Text;
using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using Identity;
using Jose;
using Xrpc;

namespace atompds.Middleware;

public record AuthVerifierConfig(string JwtKey, string AdminPass, string PublicUrl, string PdsDid);

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
    public AuthVerifier(AccountRepository accountRepository, IdResolver idResolver, AuthVerifierConfig config)
    {
        _accountRepository = accountRepository;
        _idResolver = idResolver;
        _config = config;
    }

    public async Task<AccessOutput> AccessStandard(HttpContext ctx, bool checkTakenDown = false, bool checkDeactivated = false)
    {
        return await ValidateAccessToken(ctx,
        [
            ScopeMap[AuthScope.Access],
            ScopeMap[AuthScope.AppPass],
            ScopeMap[AuthScope.AppPassPrivileged]
        ], checkTakenDown, checkDeactivated);
    }

    public async Task<AccessOutput> AccessFull(HttpContext ctx, bool checkTakenDown = false, bool checkDeactivated = false)
    {
        return await ValidateAccessToken(ctx,
        [
            ScopeMap[AuthScope.Access]
        ], checkTakenDown, checkDeactivated);
    }

    public async Task<AccessOutput> AccessPrivileged(HttpContext ctx, bool checkTakenDown = false, bool checkDeactivated = false)
    {
        return await ValidateAccessToken(ctx,
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
        });

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
            IsPrivileged = false
        }, result.Token);
    }

    public async Task<AccessOutput> ValidateAccessToken(HttpContext ctx, string[] scopes, bool checkTakenDown = false, bool checkDeactivated = false)
    {
        if (ctx.Response.HasStarted)
        {
            throw new Exception("Response has already started");
        }
        // set auth headers on response

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
                throw new XRPCError(new InvalidTokenErrorDetail("DPOP tokens are not currently supported"));
            default:
                throw new XRPCError(new InvalidTokenErrorDetail("Unexpected authorization type"));
        }

        if (checkTakenDown || checkDeactivated)
        {
            var found = await _accountRepository.GetAccount(accessOutput.AccessCredentials.Did, new AvailabilityFlags(checkTakenDown, checkDeactivated));
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
        });

        return new AccessOutput(new AccessCredentials
        {
            Did = validated.Did,
            Scope = validated.Scope,
            Audience = validated.Audience,
            IsPrivileged = false
        }, validated.Token);
    }

    private ValidatedBearer ValidateBearerToken(ParsedAuthHeader auth, string[] scopes, VerifyOptions options)
    {
        if (auth.Type != AuthType.BEARER)
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

        if (data.ContainsKey("cnf"))
        {
            // Proof-of-Possession (PoP) tokens are not allowed here
            // https://www.rfc-editor.org/rfc/rfc7800.html
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
    public Task<AdminOutput> AdminToken(HttpContext context)
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