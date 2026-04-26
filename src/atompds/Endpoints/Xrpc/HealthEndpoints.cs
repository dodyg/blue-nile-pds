using System.Text.Json.Serialization;
using AccountManager.Db;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace atompds.Endpoints.Xrpc;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("_health", HandleAsync).DisableRateLimiting();
        return group;
    }

    private static async Task<IResult> HandleAsync(IServiceScopeFactory scopeFactory)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountManagerDb>();
        if (!await db.Database.CanConnectAsync())
        {
            return Results.StatusCode(503);
        }
        return Results.Ok(new HealthResponse(StaticConfig.Version));
    }

    public record HealthResponse(
        [property: JsonPropertyName("version")]
        string Version);
}
