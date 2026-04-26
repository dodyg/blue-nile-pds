using atompds.Config;
using Config;

namespace atompds.Endpoints;

public static class RootEndpoints
{
    public static WebApplication MapRootEndpoints(
        this WebApplication app,
        ServerEnvironment environment,
        ServiceConfig serviceConfig,
        IdentityConfig identityConfig)
    {
        var version = typeof(RootEndpoints).Assembly.GetName().Version!.ToString(3);

        app.MapGet("/", () => Results.Json(new
        {
            serviceName = environment.PDS_SERVICE_NAME,
            did = serviceConfig.Did,
            version,
            publicUrl = serviceConfig.PublicUrl,
            availableUserDomains = identityConfig.ServiceHandleDomains,
            contactEmail = environment.PDS_CONTACT_EMAIL,
            logoUrl = environment.PDS_LOGO_URL,
            links = new
            {
                home = environment.PDS_HOME_URL ?? serviceConfig.PublicUrl,
                support = environment.PDS_SUPPORT_URL,
                privacyPolicy = environment.PDS_PRIVACY_POLICY_URL,
                termsOfService = environment.PDS_TERMS_OF_SERVICE_URL
            }
        }));

        app.MapGet("/robots.txt", () => "User-agent: *\nAllow: /xrpc/\nDisallow: /");

        app.MapGet("/tls-check", (HttpContext ctx) =>
        {
            var proto = ctx.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? ctx.Request.Scheme;
            return Results.Ok(new { proto, host = ctx.Request.Host.Host });
        });

        return app;
    }
}
