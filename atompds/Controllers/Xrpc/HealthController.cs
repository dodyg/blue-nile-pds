using atompds.Controllers.Xrpc.Com.Atproto;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc;

[ApiController]
[Route("xrpc")]
public class HealthController : ControllerBase
{
    [HttpGet("_health")]
    public IActionResult GetHealth()
    {
        return Ok(new IdentityController.HealthResponse(StaticConfig.Version));
    }
}