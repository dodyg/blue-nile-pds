using AccountManager;
using atompds.Middleware;
using atompds.Services;
using atompds.Services.OAuth;
using Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Xrpc;

namespace atompds.Controllers.OAuth;

[ApiController]
public class OAuthAuthorizeController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly EntrywayRelayService _entrywayRelayService;
    private readonly ILogger<OAuthAuthorizeController> _logger;
    private readonly SecretsConfig _secretsConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly OAuthSessionStore _sessionStore;

    public OAuthAuthorizeController(
        OAuthSessionStore sessionStore,
        AccountRepository accountRepository,
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig,
        EntrywayRelayService entrywayRelayService,
        ILogger<OAuthAuthorizeController> logger)
    {
        _sessionStore = sessionStore;
        _accountRepository = accountRepository;
        _serviceConfig = serviceConfig;
        _secretsConfig = secretsConfig;
        _entrywayRelayService = entrywayRelayService;
        _logger = logger;
    }

    [HttpGet("oauth/authorize")]
    public async Task<IActionResult> AuthorizeAsync(
        [FromQuery] string? client_id,
        [FromQuery] string? redirect_uri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery] string? code_challenge,
        [FromQuery] string? code_challenge_method,
        [FromQuery] string? login_hint,
        [FromQuery] string? response_type)
    {
        if (_entrywayRelayService.IsConfigured)
        {
            return Redirect(_entrywayRelayService.BuildAbsoluteUrl($"/oauth/authorize{Request.QueryString}"));
        }

        if (string.IsNullOrWhiteSpace(client_id))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required" });
        if (string.IsNullOrWhiteSpace(redirect_uri))
            return BadRequest(new { error = "invalid_request", error_description = "redirect_uri is required" });
        if (string.IsNullOrWhiteSpace(code_challenge))
            return BadRequest(new { error = "invalid_request", error_description = "code_challenge is required (PKCE)" });
        if (response_type != "code")
            return BadRequest(new { error = "unsupported_response_type", error_description = "Only 'code' response type is supported" });

        scope ??= "atproto";
        code_challenge_method ??= "S256";
        if (code_challenge_method != "S256")
            return BadRequest(new { error = "invalid_request", error_description = "Only S256 code_challenge_method is supported" });

        var auth = _sessionStore.CreateAuthorization(
            client_id, redirect_uri, scope, state ?? "",
            code_challenge, code_challenge_method, login_hint);

        var verifier = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        string? did = null;
        if (!string.IsNullOrWhiteSpace(verifier))
        {
            try
            {
                var authVerifier = HttpContext.RequestServices.GetRequiredService<AuthVerifier>();
                var accessOutput = await authVerifier.AccessStandardAsync(HttpContext);
                did = accessOutput.AccessCredentials.Did;
            }
            catch
            {
            }
        }

        if (did != null)
        {
            var oauthCode = _sessionStore.IssueCode(auth.Id, did, scope);
            var redirectParams = new Dictionary<string, string?>
            {
                ["code"] = oauthCode.Code,
                ["state"] = state,
                ["iss"] = _serviceConfig.Did
            };
            var redirectUrl = QueryHelpers.AddQueryString(redirect_uri, redirectParams);
            return Redirect(redirectUrl);
        }

        return Ok(new
        {
            authorization_id = auth.Id,
            client_id = auth.ClientId,
            scope = auth.Scope,
            login_hint = auth.LoginHint
        });
    }

    [HttpPost("oauth/authorize/consent")]
    [AccessStandard]
    public IActionResult Consent([FromBody] ConsentRequest request)
    {
        if (_entrywayRelayService.IsConfigured)
        {
            return NotFound();
        }

        var auth = _sessionStore.GetAuthorization(request.AuthorizationId);
        if (auth == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid or expired authorization"));

        var accessOutput = HttpContext.GetAuthOutput();
        var did = accessOutput.AccessCredentials.Did;

        var oauthCode = _sessionStore.IssueCode(auth.Id, did, auth.Scope);

        var redirectParams = new Dictionary<string, string?>
        {
            ["code"] = oauthCode.Code,
            ["state"] = auth.State,
            ["iss"] = _serviceConfig.Did
        };
        var redirectUrl = QueryHelpers.AddQueryString(auth.RedirectUri, redirectParams);
        return Ok(new { redirect = redirectUrl });
    }
}

public record ConsentRequest(string AuthorizationId);
