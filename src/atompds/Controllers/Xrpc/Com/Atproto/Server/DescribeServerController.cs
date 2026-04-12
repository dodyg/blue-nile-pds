using atompds.Config;
using Config;
using CarpaNet;
using ComAtproto.Server;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class DescribeServerController : ControllerBase
{
    private readonly IdentityConfig _identityConfig;
    private readonly InvitesConfig _invitesConfig;
    private readonly ServerEnvironment _serverEnvironment;
    private readonly ServiceConfig _serviceConfig;

    public DescribeServerController(
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        InvitesConfig invitesConfig,
        ServerEnvironment serverEnvironment)
    {
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _invitesConfig = invitesConfig;
        _serverEnvironment = serverEnvironment;
    }

    [HttpGet("com.atproto.server.describeServer")]
    public IActionResult DescribeServer()
    {
        DescribeServerLinks? links = null;
        if (_serverEnvironment.PDS_PRIVACY_POLICY_URL != null || _serverEnvironment.PDS_TERMS_OF_SERVICE_URL != null)
        {
            links = new DescribeServerLinks
            {
                PrivacyPolicy = _serverEnvironment.PDS_PRIVACY_POLICY_URL,
                TermsOfService = _serverEnvironment.PDS_TERMS_OF_SERVICE_URL
            };
        }

        DescribeServerContact? contact = null;
        if (_serverEnvironment.PDS_CONTACT_EMAIL != null)
        {
            contact = new DescribeServerContact
            {
                Email = _serverEnvironment.PDS_CONTACT_EMAIL
            };
        }

        return Ok(new DescribeServerOutput
        {
            Did = new ATDid(_serviceConfig.Did),
            AvailableUserDomains = _identityConfig.ServiceHandleDomains.ToList(),
            InviteCodeRequired = _invitesConfig.Required,
            Links = links,
            Contact = contact,
            PhoneVerificationRequired = _serverEnvironment.PDS_PHONE_VERIFICATION_REQUIRED ? true : null
        });
    }
}
