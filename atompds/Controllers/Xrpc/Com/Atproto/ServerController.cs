using System.Net.Mail;
using System.Text;
using atompds.Model;
using atompds.Pds.Config;
using atompds.Pds.Handle;
using FishyFlip.Lexicon.Com.Atproto.Server;
using FishyFlip.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace atompds.Controllers.Xrpc.Com.Atproto;

[ApiController]
[Route("xrpc")]
public class ServerController : ControllerBase
{
    private readonly ILogger<ServerController> _logger;
    private readonly AccountManager.AccountManager _accountManager;
    private readonly IdentityConfig _identityConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly JwtHandler _jwtHandler;
    private readonly InvitesConfig _invitesConfig;
    private readonly HttpClient _httpClient;
    private readonly Handle _handle;

    public ServerController(ILogger<ServerController> logger, 
        AccountManager.AccountManager accountManager,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        JwtHandler jwtHandler,
        InvitesConfig invitesConfig,
        HttpClient httpClient,
        Handle handle)
    {
        _logger = logger;
        _accountManager = accountManager;
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _jwtHandler = jwtHandler;
        _invitesConfig = invitesConfig;
        _httpClient = httpClient;
        _handle = handle;
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
    
    [HttpPost("com.atproto.server.createSession")]
    [EnableRateLimiting(Program.FixedWindowLimiterName)]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionInput request)
    {
        try
        {
            var response = await _jwtHandler.GenerateJwtToken(request);
            return Ok(response);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create session");
            return Unauthorized();
        }
    }
}