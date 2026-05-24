using atompds.Services;

namespace atompds.Endpoints.OAuth;

public static class OAuthClientMetadataEndpoints
{
    public static WebApplication MapOAuthClientMetadataEndpoints(this WebApplication app)
    {
        app.MapGet("oauth/client-metadata.json", Handle);
        return app;
    }

    private static IResult Handle(
        HttpContext context,
        EntrywayRelayService entrywayRelayService,
        ILogger<Program> logger,
        string? client_id)
    {
        if (entrywayRelayService.IsConfigured)
        {
            return Results.Redirect(entrywayRelayService.BuildAbsoluteUrl($"/oauth/client-metadata.json{context.Request.QueryString}"));
        }

        if (string.IsNullOrWhiteSpace(client_id))
        {
            return Results.BadRequest(new { error = "invalid_request", error_description = "client_id is required" });
        }

        try
        {
            if (client_id.StartsWith("http://") || client_id.StartsWith("https://"))
            {
                return Results.Redirect(client_id);
            }

            return Results.NotFound(new { error = "invalid_client", error_description = "Client not found" });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to resolve client metadata for {ClientId}", client_id);
            return Results.BadRequest(new { error = "invalid_client", error_description = "Failed to resolve client metadata" });
        }
    }
}
