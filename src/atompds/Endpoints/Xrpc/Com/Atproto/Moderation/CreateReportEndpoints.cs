using System.Net.Http.Headers;
using System.Text.Json;
using atompds.Config;
using atompds.Middleware;
using atompds.Services;
using Config;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Moderation;

public static class CreateReportEndpoints
{
    public static RouteGroupBuilder MapCreateReportEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.moderation.createReport", HandleAsync).WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        JsonElement input,
        HttpClient httpClient,
        ServerEnvironment serverEnvironment,
        ServiceJwtBuilder serviceJwtBuilder,
        ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(serverEnvironment.PDS_REPORT_SERVICE_URL) ||
            string.IsNullOrWhiteSpace(serverEnvironment.PDS_REPORT_SERVICE_DID))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Report service is not configured"));
        }

        try
        {
            var auth = context.GetAuthOutput();
            var jwt = serviceJwtBuilder.CreateServiceJwt(
                auth.AccessCredentials.Did,
                serverEnvironment.PDS_REPORT_SERVICE_DID,
                "com.atproto.moderation.createReport");

            var url = $"{serverEnvironment.PDS_REPORT_SERVICE_URL}/xrpc/com.atproto.moderation.createReport";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            request.Content = new StringContent(input.GetRawText(), System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
            await context.Response.WriteAsync(content);
            return Results.Empty;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to forward moderation report");
            throw;
        }
    }
}
