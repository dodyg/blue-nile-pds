using atompds.Config;
using CarpaNet;
using ComAtproto.Server;
using Config;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class DescribeServerEndpoints
{
    public static RouteGroupBuilder MapDescribeServerEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.server.describeServer", Handle);
        return group;
    }

    private static IResult Handle(
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        InvitesConfig invitesConfig,
        ServerEnvironment serverEnvironment)
    {
        DescribeServerLinks? links = null;
        if (serverEnvironment.PDS_PRIVACY_POLICY_URL != null || serverEnvironment.PDS_TERMS_OF_SERVICE_URL != null)
        {
            links = new DescribeServerLinks
            {
                PrivacyPolicy = serverEnvironment.PDS_PRIVACY_POLICY_URL,
                TermsOfService = serverEnvironment.PDS_TERMS_OF_SERVICE_URL
            };
        }

        DescribeServerContact? contact = null;
        if (serverEnvironment.PDS_CONTACT_EMAIL != null)
        {
            contact = new DescribeServerContact { Email = serverEnvironment.PDS_CONTACT_EMAIL };
        }

        return Results.Ok(new DescribeServerOutput
        {
            Did = new ATDid(serviceConfig.Did),
            AvailableUserDomains = identityConfig.ServiceHandleDomains.ToList(),
            InviteCodeRequired = invitesConfig.Required,
            Links = links,
            Contact = contact,
            PhoneVerificationRequired = serverEnvironment.PDS_PHONE_VERIFICATION_REQUIRED ? true : null
        });
    }
}
