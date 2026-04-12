using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace atompds.Controllers.OAuth;

[ApiController]
public class OAuthClientRegistrationController : ControllerBase
{
    private readonly ILogger<OAuthClientRegistrationController> _logger;

    public OAuthClientRegistrationController(ILogger<OAuthClientRegistrationController> logger)
    {
        _logger = logger;
    }

    [HttpGet("oauth/client-metadata.json")]
    public IActionResult GetClientMetadata([FromQuery] string? client_id)
    {
        if (string.IsNullOrWhiteSpace(client_id))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required" });

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
