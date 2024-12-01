using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc;

[ApiController]
[Route("xrpc")]
public class HealthController : ControllerBase
{
    [HttpGet("_health")]
    public IActionResult GetHealth()
    {
        return Ok(new HealthResponse(StaticConfig.Version));
    }
    
    public record HealthResponse(
        [property: JsonPropertyName("version")]
        string Version);
}