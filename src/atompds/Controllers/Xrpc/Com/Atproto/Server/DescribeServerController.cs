using Config;
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
    private readonly InvitesConfig _invitesConfig;

    public DescribeServerController(IdentityConfig identityConfig, ServiceConfig serviceConfig, InvitesConfig invitesConfig)
    {
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _invitesConfig = invitesConfig;
    }
    
    [HttpGet("com.atproto.server.describeServer")]
    public IActionResult DescribeServer()
    {
        return Ok(new DescribeServerOutput
        {
            Did = new ATDid(_serviceConfig.Did),
            AvailableUserDomains = _identityConfig.ServiceHandleDomains.ToList(),
            InviteCodeRequired = _invitesConfig.Required
        });
    }
}