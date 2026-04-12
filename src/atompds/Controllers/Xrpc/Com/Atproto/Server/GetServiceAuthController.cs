using atompds.Middleware;
using atompds.Services;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class GetServiceAuthController : ControllerBase
{
    private readonly ILogger<GetServiceAuthController> _logger;
    private readonly ServiceJwtBuilder _serviceJwtBuilder;

    public GetServiceAuthController(ServiceJwtBuilder serviceJwtBuilder, ILogger<GetServiceAuthController> logger)
    {
        _serviceJwtBuilder = serviceJwtBuilder;
        _logger = logger;
    }

    [HttpGet("com.atproto.server.getServiceAuth")]
    [AccessStandard]
    public IActionResult GetServiceAuth([FromQuery] string? aud, [FromQuery] string? lxm)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(aud))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("aud is required"));
        }

        var token = _serviceJwtBuilder.CreateServiceJwt(did, aud, lxm);

        return Ok(new
        {
            token
        });
    }
}
