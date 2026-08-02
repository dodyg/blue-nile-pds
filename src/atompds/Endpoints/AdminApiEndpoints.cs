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
