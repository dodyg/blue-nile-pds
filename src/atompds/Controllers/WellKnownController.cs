using System.Text.Json.Serialization;
using AccountManager;
using Config;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers;

[ApiController]
[Route(".well-known")]
public class WellKnownController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<WellKnownController> _logger;
    private readonly ServiceConfig _serviceConfig;
    public WellKnownController(ServiceConfig serviceConfig,
        AccountRepository accountRepository,
        ILogger<WellKnownController> logger)
    {
        _serviceConfig = serviceConfig;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpGet("oauth-protected-resource")]
    public IActionResult GetOAuthProtectedResource()
    {
        return Ok(new WellProtectedResourceResponse(
            _serviceConfig.PublicUrl,
            [_serviceConfig.PublicUrl], // we are our own auth server
            [],
            ["header"],
            "https://atproto.com"));
    }

    [HttpGet("oauth-authorization-server")]
    public IActionResult GetOAuthAuthorizationServer()
    {
        var baseUrl = _serviceConfig.PublicUrl;
        return Ok(new
        {
            issuer = _serviceConfig.Did,
            scopes_supported = new[] { "atproto", "transition:generic" },
            scopes_documentation = "https://atproto.com/specs/oauth",
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            code_challenge_methods_supported = new[] { "S256" },
            authorization_endpoint = $"{baseUrl}/oauth/authorize",
            token_endpoint = $"{baseUrl}/oauth/token",
            token_endpoint_auth_methods_supported = new[] { "none" },
            dpop_signing_alg_values_supported = new[] { "ES256" },
            client_id_metadata_document = "https://atproto.com/specs/oauth#client-id-metadata-document"
        });
    }

    [HttpGet("atproto-did")]
    public async Task<IActionResult> GetAtprotoDidAsync()
    {
        // get hostname for request
        var host = Request.Host.Host;
        // _logger.LogInformation("Resolving handle {Handle}", host);
        var acc = await _accountRepository.GetAccountAsync(host);
        if (acc?.Handle == null)
        {
            return NotFound("no user by that handle exists on this PDS");
        }

        //return Content($"did:web:{acc.Handle}");
        return Content(acc.Did);
    }

    public record WellProtectedResourceResponse(
        [property: JsonPropertyName("resource")]
        string Resource,
        [property: JsonPropertyName("authorization_servers")]
        string[] AuthorizationServers,
        [property: JsonPropertyName("scopes_supported")]
        string[] ScopesSupported,
        [property: JsonPropertyName("bearer_methods_supported")]
        string[] BearerMethodsSupported,
        [property: JsonPropertyName("resource_documentation")]
        string ResourceDocumentation);
}
