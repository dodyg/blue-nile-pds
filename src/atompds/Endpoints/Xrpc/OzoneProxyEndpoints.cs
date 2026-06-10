using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using atompds.Config;
using atompds.Middleware;
using atompds.Services;
using Config;
using Identity;
using Xrpc;

namespace atompds.Endpoints.Xrpc;

public static class OzoneProxyEndpoints
{
    public static RouteGroupBuilder MapOzoneProxyEndpoints(this RouteGroupBuilder group)
    {
        var methods = new[]
        {
            "tools.ozone.moderation.emitEvent",
            "tools.ozone.moderation.getEvent",
            "tools.ozone.moderation.getRecord",
            "tools.ozone.moderation.getRepo",
            "tools.ozone.moderation.queryEvents",
            "tools.ozone.moderation.queryStatuses",
            "tools.ozone.moderation.scheduleAction",
            "tools.ozone.moderation.cancelScheduledActions",
            "tools.ozone.moderation.listScheduledActions",
            "tools.ozone.moderation.getAccountTimeline",
            "tools.ozone.moderation.searchRepos",
            "tools.ozone.communication.createTemplate",
            "tools.ozone.communication.deleteTemplate",
            "tools.ozone.communication.listTemplates",
            "tools.ozone.communication.updateTemplate",
            "tools.ozone.safelink.addRule",
            "tools.ozone.safelink.queryEvents",
            "tools.ozone.safelink.queryRules",
            "tools.ozone.safelink.removeRule",
            "tools.ozone.safelink.updateRule",
            "tools.ozone.team.addMember",
            "tools.ozone.team.deleteMember",
            "tools.ozone.team.listMembers",
            "tools.ozone.team.updateMember",
            "tools.ozone.verification.grantVerifications",
            "tools.ozone.verification.listVerifications",
            "tools.ozone.verification.revokeVerifications",
        };

        foreach (var method in methods)
        {
            var parts = method.Split('.');
            var httpMethod = method.StartsWith("tools.ozone.moderation.emitEvent") ||
                             method.StartsWith("tools.ozone.communication.createTemplate") ||
                             method.StartsWith("tools.ozone.communication.deleteTemplate") ||
                             method.StartsWith("tools.ozone.communication.updateTemplate") ||
                             method.StartsWith("tools.ozone.safelink.addRule") ||
                             method.StartsWith("tools.ozone.safelink.removeRule") ||
                             method.StartsWith("tools.ozone.safelink.updateRule") ||
                             method.StartsWith("tools.ozone.team.addMember") ||
                             method.StartsWith("tools.ozone.team.deleteMember") ||
                             method.StartsWith("tools.ozone.team.updateMember") ||
                             method.StartsWith("tools.ozone.verification.grantVerifications") ||
                             method.StartsWith("tools.ozone.verification.revokeVerifications") ||
                             method.StartsWith("tools.ozone.moderation.scheduleAction") ||
                             method.StartsWith("tools.ozone.moderation.cancelScheduledActions")
                ? "POST" : "GET";

            if (httpMethod == "POST")
                group.MapPost(method, ProxyAsync).WithMetadata(new ModeratorTokenAttribute());
            else
                group.MapGet(method, ProxyAsync).WithMetadata(new ModeratorTokenAttribute());
        }

        return group;
    }

    private static async Task<IResult> ProxyAsync(
        HttpContext context,
        HttpClient client,
        ServiceJwtBuilder serviceJwtBuilder,
        ILogger<Program> logger)
    {
        var proxyConfig = context.RequestServices.GetRequiredService<ProxyConfig>();
        var modServiceUrl = context.RequestServices.GetRequiredService<ServerEnvironment>().PDS_MOD_SERVICE_URL;
        var modServiceDid = context.RequestServices.GetRequiredService<ServerEnvironment>().PDS_MOD_SERVICE_DID;

        if (string.IsNullOrWhiteSpace(modServiceUrl) || string.IsNullOrWhiteSpace(modServiceDid))
        {
            throw new XRPCError(404);
        }

        var reqNsid = AppViewProxyEndpoints.ParseUrlNsid(context.Request.Path);
        var url = $"{modServiceUrl.TrimEnd('/')}/xrpc/{reqNsid}";

        if (!proxyConfig.DisableSsrfProtection)
        {
            AppViewProxyEndpoints.ValidateUrlAgainstSsrf(url);
        }

        var auth = context.GetAuthOutput();
        var jwt = serviceJwtBuilder.CreateServiceJwt(auth.AccessCredentials.Did, modServiceDid, reqNsid);

        if (context.Request.Method == "GET")
        {
            url += context.Request.QueryString;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
            await context.Response.WriteAsync(content);
            return Results.Empty;
        }
        else
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            if (context.Request.ContentLength > 0)
            {
                var body = await context.Request.BodyReader.ReadAsync();
                request.Content = new ByteArrayContent(body.Buffer.ToArray());
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                {
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(context.Request.ContentType);
                }
            }
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
            await context.Response.WriteAsync(content);
            return Results.Empty;
        }
    }
}
