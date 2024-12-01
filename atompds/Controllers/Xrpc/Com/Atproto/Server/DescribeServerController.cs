using atompds.Pds.Config;
using FishyFlip.Lexicon.Com.Atproto.Server;
using FishyFlip.Models;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class DescribeServerController : ControllerBase
{
    private readonly IdentityConfig _identityConfig;
    private readonly ServiceConfig _serviceConfig;
    
    public DescribeServerController(IdentityConfig identityConfig, ServiceConfig serviceConfig)
    {
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
    }
    
    [HttpGet("com.atproto.server.describeServer")]
    public IActionResult DescribeServer()
    {
        return Ok(new DescribeServerOutput
        {
            Did = new ATDid(_serviceConfig.Did),
            AvailableUserDomains = _identityConfig.ServiceHandleDomains.ToList(),
            InviteCodeRequired = true
        });
    }
}