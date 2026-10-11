using BlueNilePds.Core.CommonWeb;
using BlueNilePds.Core.Handle;
using BlueNilePds.Host.Configuration;
using BlueNilePds.Pds.Config;

namespace BlueNilePds.Host.Endpoints;

public static class RootEndpoints
{
    public static WebApplication MapRootEndpoints(
        this WebApplication app,
        ServerEnvironment environment,
        ServiceConfig serviceConfig,
        IdentityConfig identityConfig)
    {
        var version = typeof(RootEndpoints).Assembly.GetName().Version!.ToString(3);

        app.MapGet("/", (HttpContext ctx, IWebHostEnvironment env) =>
        {
            // Browser visits to a user-handle subdomain (jane.domain.com) bounce
            // to that handle's bsky.app profile. Serve the public SPA for browser
            // navigations to anything else; keep the JSON service-info response
            // for API clients and non-HTML requests.
            var accept = ctx.Request.Headers.Accept.ToString();
            if (accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                var profileHandle = TryGetHandleSubdomain(ctx, environment.PDS_HOSTNAME);
                if (profileHandle != null)
                    return Results.Redirect($"https://bsky.app/profile/{profileHandle}");

                var fileInfo = env.WebRootFileProvider.GetFileInfo("index.html");
                if (fileInfo.Exists && fileInfo.PhysicalPath != null)
                    return Results.File(fileInfo.PhysicalPath, "text/html");
            }

            return Results.Json(new
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
            });
        });

        // Maps a request Host like jane.domain.com (domain.com == PDS hostname)
        // to the user handle it names. Returns null for the apex domain, foreign
        // hosts, deep subdomains, reserved labels (www, admin, ...) and anything
        // that is not a syntactically valid handle. Syntax-only: no DB lookup,
        // unknown handles land on bsky.app's profile-not-found page.
        static string? TryGetHandleSubdomain(HttpContext ctx, string baseHostname)
        {
            var host = ctx.Request.Host.Host.ToLowerInvariant().TrimEnd('.');
            var baseHost = baseHostname.ToLowerInvariant().TrimEnd('.');
            if (host.Length <= baseHost.Length + 1 ||
                !host.EndsWith($".{baseHost}", StringComparison.Ordinal))
                return null;

            var front = host[..^(baseHost.Length + 1)];
            if (front.Contains('.'))
                return null;

            if (Reserved.ReservedSubdomains.Contains(front))
                return null;

            try
            {
                Util.EnsureValidHandle(host);
            }
            catch
            {
                return null;
            }

            return host;
        }

        app.MapGet("/robots.txt", () => "User-agent: *\nAllow: /xrpc/\nDisallow: /");

        app.MapGet("/tls-check", (HttpContext ctx) =>
        {
            var proto = ctx.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? ctx.Request.Scheme;
            return Results.Ok(new { proto, host = ctx.Request.Host.Host });
        });

        return app;
    }
}