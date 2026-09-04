using BlueNilePds.Host.Middleware;
using BlueNilePds.Host.Services;

namespace BlueNilePds.Host.Endpoints.Admin;

public static class AdminBackupEndpoints
{
    public static RouteGroupBuilder MapAdminBackupEndpoints(this RouteGroupBuilder group)
    {
        var backup = group.MapGroup("/backup");
        backup.MapPost("create", HandleCreateAsync).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("status", HandleStatus).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("list", HandleList).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("download", HandleDownload).WithMetadata(new AdminTokenAttribute());
        backup.MapPost("delete", HandleDelete).WithMetadata(new AdminTokenAttribute());
        return group;
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
