using atompds.Services;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.OAuth;

[ApiController]
public class OAuthClientRegistrationController : ControllerBase
{
    private readonly EntrywayRelayService _entrywayRelayService;
    private readonly ILogger<OAuthClientRegistrationController> _logger;

    public OAuthClientRegistrationController(
        EntrywayRelayService entrywayRelayService,
        ILogger<OAuthClientRegistrationController> logger)
    {
        _entrywayRelayService = entrywayRelayService;
        _logger = logger;
    }

    [HttpGet("oauth/client-metadata.json")]
    public IActionResult GetClientMetadata([FromQuery] string? client_id)
    {
        if (_entrywayRelayService.IsConfigured)
        {
            return Redirect(_entrywayRelayService.BuildAbsoluteUrl($"/oauth/client-metadata.json{Request.QueryString}"));
        }

        if (string.IsNullOrWhiteSpace(client_id))
        {
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required" });
        }

        try
        {
            if (client_id.StartsWith("http://") || client_id.StartsWith("https://"))
            {
                return Redirect(client_id);
            }

            return NotFound(new { error = "invalid_client", error_description = "Client not found" });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to resolve client metadata for {ClientId}", client_id);
            return BadRequest(new { error = "invalid_client", error_description = "Failed to resolve client metadata" });
        }
    }
}
