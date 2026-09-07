using BlueNilePds.Host.Middleware;
using BlueNilePds.Host.Services;
using BlueNilePds.Pds.Xrpc;

namespace BlueNilePds.Host.Endpoints.Admin;

public static class AdminResyncEndpoints
{
    public static RouteGroupBuilder MapAdminResyncEndpoints(this RouteGroupBuilder group)
    {
        var resync = group.MapGroup("/repo/resync");
        resync.MapPost("", HandleResyncAsync).WithMetadata(new AdminTokenAttribute());
        resync.MapGet("status", HandleResyncStatus).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleResyncAsync(RepoResyncService resyncService, RepoResyncRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        }

        await resyncService.StartAsync(request.Did);
        return Results.Ok(new RepoResyncCreateOutput
        {
            Status = "started",
            StartedAt = DateTime.UtcNow
        });
    }

    private static IResult HandleResyncStatus(RepoResyncService resyncService)
    {
        var state = resyncService.GetStatus();
        return Results.Ok(new RepoResyncStatusOutput
        {
            Status = state.Status.ToString().ToLowerInvariant(),
            Did = state.Did,
            StartedAt = state.StartedAt,
            CompletedAt = state.CompletedAt,
            RecordsScanned = state.RecordsScanned,
            RecordsRewritten = state.RecordsRewritten,
            Error = state.Error
        });
    }
}
