using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;

namespace atompds.Endpoints.Xrpc;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("_health", Handle).DisableRateLimiting();
        return group;
    }

    private static IResult Handle()
    {
        return Results.Ok(new HealthResponse(StaticConfig.Version));
    }

    public record HealthResponse(
        [property: JsonPropertyName("version")]
        string Version);
}
