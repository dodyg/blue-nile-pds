using AccountManager;
using atompds.Middleware;
using atompds.Config;
using atompds.Services;
using atompds.Services.OAuth;
using Config;
using Microsoft.AspNetCore.WebUtilities;
using Xrpc;

namespace atompds.Endpoints.OAuth;

public static class OAuthAuthorizeEndpoints
{
    public static WebApplication MapOAuthAuthorizeEndpoints(this WebApplication app)
    {
        app.MapGet("oauth/authorize", AuthorizeAsync);
        app.MapPost("oauth/authorize/consent", Consent).WithMetadata(new AccessStandardAttribute());
        return app;
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext context,
        OAuthSessionStore sessionStore,
        AccountRepository accountRepository,
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig,
        ServerEnvironment serverEnvironment,
        EntrywayRelayService entrywayRelayService,
        string? client_id,
        string? redirect_uri,
        string? scope,
        string? state,
        string? code_challenge,
        string? code_challenge_method,
        string? login_hint,
        string? prompt,
        string? response_type)
    {
        var trustedClientIds = new HashSet<string>(serverEnvironment.PDS_OAUTH_TRUSTED_CLIENTS, StringComparer.Ordinal);

        if (entrywayRelayService.IsConfigured)
        {
            return Results.Redirect(entrywayRelayService.BuildAbsoluteUrl($"/oauth/authorize{context.Request.QueryString}"));
        }

        if (string.IsNullOrWhiteSpace(client_id))
            return Results.BadRequest(new { error = "invalid_request", error_description = "client_id is required" });
        if (string.IsNullOrWhiteSpace(redirect_uri))
            return Results.BadRequest(new { error = "invalid_request", error_description = "redirect_uri is required" });
        if (string.IsNullOrWhiteSpace(code_challenge))
            return Results.BadRequest(new { error = "invalid_request", error_description = "code_challenge is required (PKCE)" });
        if (response_type != "code")
            return Results.BadRequest(new { error = "unsupported_response_type", error_description = "Only 'code' response type is supported" });

        scope ??= "atproto";
        code_challenge_method ??= "S256";
        if (code_challenge_method != "S256")
            return Results.BadRequest(new { error = "invalid_request", error_description = "Only S256 code_challenge_method is supported" });

        var auth = sessionStore.CreateAuthorization(
            client_id, redirect_uri, scope, state ?? "",
            code_challenge, code_challenge_method, login_hint);

        var verifier = context.Request.Headers["Authorization"].FirstOrDefault();
        string? did = null;
        if (!string.IsNullOrWhiteSpace(verifier))
        {
            try
            {
                var authVerifier = context.RequestServices.GetRequiredService<AuthVerifier>();
                var accessOutput = await authVerifier.AccessStandardAsync(context);
                did = accessOutput.AccessCredentials.Did;
            }
            catch
            {
            }
        }

        var isTrustedClient = trustedClientIds.Contains(client_id);
        if (!isTrustedClient && string.Equals(prompt, "none", StringComparison.Ordinal))
        {
            return Results.BadRequest(new
            {
                error = "consent_required",
                error_description = "Public clients must complete interactive consent"
            });
        }

        if (did != null && isTrustedClient)
        {
            var oauthCode = sessionStore.IssueCode(auth.Id, did, scope);
            var redirectParams = new Dictionary<string, string?>
            {
                ["code"] = oauthCode.Code,
                ["state"] = state,
                ["iss"] = serviceConfig.PublicUrl
            };
            var redirectUrl = QueryHelpers.AddQueryString(redirect_uri, redirectParams);
            return Results.Redirect(redirectUrl);
        }

        return Results.Ok(new
        {
            authorization_id = auth.Id,
            client_id = auth.ClientId,
            scope = auth.Scope,
            login_hint = auth.LoginHint
        });
    }

    private static IResult Consent(
        HttpContext context,
        OAuthSessionStore sessionStore,
        ServiceConfig serviceConfig,
        EntrywayRelayService entrywayRelayService,
        ConsentRequest request)
    {
        if (entrywayRelayService.IsConfigured)
        {
            return Results.NotFound();
        }

        var authSession = sessionStore.GetAuthorization(request.AuthorizationId);
        if (authSession == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid or expired authorization"));

        var accessOutput = context.GetAuthOutput();
        var did = accessOutput.AccessCredentials.Did;

        var oauthCode = sessionStore.IssueCode(authSession.Id, did, authSession.Scope);

        var redirectParams = new Dictionary<string, string?>
        {
            ["code"] = oauthCode.Code,
            ["state"] = authSession.State,
            ["iss"] = serviceConfig.PublicUrl
        };
        var redirectUrl = QueryHelpers.AddQueryString(authSession.RedirectUri, redirectParams);
        return Results.Ok(new { redirect = redirectUrl });
    }
}

public record ConsentRequest(string AuthorizationId);
