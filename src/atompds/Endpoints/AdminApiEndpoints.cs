using System.Text.Json.Serialization;
using atompds.Middleware;
using atompds.Services;
using Xrpc;

namespace atompds.Endpoints;

public static class AdminApiEndpoints
{
    public static WebApplication MapAdminApiEndpoints(this WebApplication app)
    {
        var backup = app.MapGroup("admin/api/backup");
        backup.MapPost("create", HandleCreateAsync).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("status", HandleStatus).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("list", HandleList).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("download", HandleDownload).WithMetadata(new AdminTokenAttribute());
        backup.MapPost("delete", HandleDelete).WithMetadata(new AdminTokenAttribute());

        var resync = app.MapGroup("admin/api/repo/resync");
        resync.MapPost("", HandleResyncAsync).WithMetadata(new AdminTokenAttribute());
        resync.MapGet("status", HandleResyncStatus).WithMetadata(new AdminTokenAttribute());
        return app;
    }

    private static async Task<IResult> HandleCreateAsync(BackupService backupService)
    {
        await backupService.CreateBackupAsync();
        return Results.Ok(new BackupCreateOutput
        {
            Status = "started",
            StartedAt = DateTime.UtcNow
        });
    }

    private static IResult HandleStatus(BackupService backupService)
    {
        var state = backupService.GetStatus();
        return Results.Ok(new BackupStatusOutput
        {
            Status = state.Status.ToString().ToLowerInvariant(),
            StartedAt = state.StartedAt,
            CompletedAt = state.CompletedAt,
            FileName = state.FileName,
            SizeBytes = state.SizeBytes,
            Error = state.Error
        });
    }

    private static IResult HandleList(BackupService backupService)
    {
        var backups = backupService.ListBackups();
        return Results.Ok(new BackupListOutput
        {
            Backups = backups.Select(b => new BackupEntryOutput
            {
                FileName = b.FileName,
                CreatedAt = b.CreatedAt,
                SizeBytes = b.SizeBytes
            }).ToList()
        });
    }

    private static IResult HandleDownload(BackupService backupService, string? fileName)
    {
        var path = backupService.GetBackupPath(fileName ?? "");
        return Results.File(path, "application/zip", fileName, enableRangeProcessing: true);
    }

    private static IResult HandleDelete(BackupService backupService, BackupDeleteRequest request)
    {
        backupService.DeleteBackup(request.FileName);
        return Results.NoContent();
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

public record BackupCreateOutput
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; init; }
}

public record BackupStatusOutput
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; init; }
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; init; }
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }
    [JsonPropertyName("sizeBytes")]
    public long? SizeBytes { get; init; }
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

public record BackupListOutput
{
    [JsonPropertyName("backups")]
    public List<BackupEntryOutput> Backups { get; init; } = [];
}

public record BackupEntryOutput
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = "";
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }
}

public record BackupDeleteRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = "";
}

public record RepoResyncRequest
{
    [JsonPropertyName("did")]
    public string Did { get; init; } = "";
}

public record RepoResyncCreateOutput
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; init; }
}

public record RepoResyncStatusOutput
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    [JsonPropertyName("did")]
    public string? Did { get; init; }
    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; init; }
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; init; }
    [JsonPropertyName("recordsScanned")]
    public int RecordsScanned { get; init; }
    [JsonPropertyName("recordsRewritten")]
    public int RecordsRewritten { get; init; }
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
