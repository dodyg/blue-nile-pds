using System.Text.Json.Serialization;
using BlueNilePds.Host.Middleware;
using BlueNilePds.Host.Services;
using BlueNilePds.Pds.Sequencer.Db;
using BlueNilePds.Pds.Xrpc;
using Microsoft.EntityFrameworkCore;

namespace BlueNilePds.Host.Endpoints;

public static class AdminApiEndpoints
{
    public static WebApplication MapAdminApiEndpoints(this WebApplication app)
    {
        var backup = app.MapGroup("api/admin/backup");
        backup.MapPost("create", HandleCreateAsync).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("status", HandleStatus).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("list", HandleList).WithMetadata(new AdminTokenAttribute());
        backup.MapGet("download", HandleDownload).WithMetadata(new AdminTokenAttribute());
        backup.MapPost("delete", HandleDelete).WithMetadata(new AdminTokenAttribute());

        var resync = app.MapGroup("api/admin/repo/resync");
        resync.MapPost("", HandleResyncAsync).WithMetadata(new AdminTokenAttribute());
        resync.MapGet("status", HandleResyncStatus).WithMetadata(new AdminTokenAttribute());

        var export = app.MapGroup("api/admin/export");
        export.MapGet("user", HandleExportUserAsync).WithMetadata(new AdminTokenAttribute());

        var sequencer = app.MapGroup("api/admin/sequencer");
        sequencer.MapGet("status", HandleSequencerStatusAsync).WithMetadata(new AdminTokenAttribute());
        sequencer.MapPost("advance", HandleSequencerAdvanceAsync).WithMetadata(new AdminTokenAttribute());
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

    private static async Task<IResult> HandleExportUserAsync(
        string? did,
        UserDataExportService exportService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));

        var (resolvedDid, handle) = await exportService.ResolveAccountAsync(did.Trim());
        var fileName = UserDataExportService.BuildFileName(handle);

        // Buffer the zip in memory: ZipArchive performs synchronous writes
        // internally, which Kestrel's response stream disallows.
        // Per-user exports are small enough that buffering is safe.
        using var buffer = new MemoryStream();
        await exportService.ExportToStreamAsync(resolvedDid, handle, buffer, cancellationToken);

        return Results.File(buffer.ToArray(), "application/zip", fileName);
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

    private static async Task<IResult> HandleSequencerStatusAsync(IDbContextFactory<SequencerDb> seqDbFactory)
    {
        await using var db = await seqDbFactory.CreateDbContextAsync();
        var current = await db.RepoSeqs.MaxAsync(x => (int?)x.Seq);
        var earliest = await db.RepoSeqs.MinAsync(x => (int?)x.Seq);
        var count = await db.RepoSeqs.CountAsync();
        var earliestTime = await db.RepoSeqs.MinAsync(x => (DateTime?)x.SequencedAt);
        var latestTime = await db.RepoSeqs.MaxAsync(x => (DateTime?)x.SequencedAt);
        var counter = await db.Database
            .SqlQueryRaw<int?>("SELECT seq AS Value FROM sqlite_sequence WHERE name = 'RepoSeqs'")
            .FirstOrDefaultAsync();
        return Results.Ok(new SequencerStatusOutput
        {
            CurrentSeq = current,
            EarliestSeq = earliest,
            EventCount = count,
            EarliestTime = earliestTime,
            LatestTime = latestTime,
            SequenceCounter = counter
        });
    }

    private static async Task<IResult> HandleSequencerAdvanceAsync(
        IDbContextFactory<SequencerDb> seqDbFactory,
        SequencerAdvanceRequest request,
        ILogger<Program> logger)
    {
        if (request.TargetSeq < 1 || request.TargetSeq == int.MaxValue)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("targetSeq must be a positive integer below 2147483647"));
        }

        await using var db = await seqDbFactory.CreateDbContextAsync();
        var current = await db.RepoSeqs.MaxAsync(x => (int?)x.Seq);
        if (request.TargetSeq <= (current ?? 0))
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"targetSeq must be greater than current max seq {current ?? 0}"));
        }

        // Advance the AUTOINCREMENT counter so the next sequenced event gets
        // targetSeq + 1. Used to recover firehose consumers holding a cursor
        // ahead of this host (e.g. after a database restore). Never moves
        // backwards: that would collide with existing seq primary keys.
        var updated = await db.Database.ExecuteSqlRawAsync(
            "UPDATE sqlite_sequence SET seq = {0} WHERE name = 'RepoSeqs'", request.TargetSeq);
        if (updated == 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO sqlite_sequence (name, seq) VALUES ('RepoSeqs', {0})", request.TargetSeq);
        }

        logger.LogWarning("Sequencer counter advanced from {Previous} to {New} by admin", current, request.TargetSeq);
        return Results.Ok(new SequencerAdvanceOutput
        {
            PreviousSeq = current,
            NewSeq = request.TargetSeq
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

public record SequencerAdvanceRequest
{
    [JsonPropertyName("targetSeq")]
    public int TargetSeq { get; init; }
}

public record SequencerStatusOutput
{
    [JsonPropertyName("currentSeq")]
    public int? CurrentSeq { get; init; }
    [JsonPropertyName("earliestSeq")]
    public int? EarliestSeq { get; init; }
    [JsonPropertyName("eventCount")]
    public int EventCount { get; init; }
    [JsonPropertyName("earliestTime")]
    public DateTime? EarliestTime { get; init; }
    [JsonPropertyName("latestTime")]
    public DateTime? LatestTime { get; init; }
    [JsonPropertyName("sequenceCounter")]
    public int? SequenceCounter { get; init; }
}

public record SequencerAdvanceOutput
{
    [JsonPropertyName("previousSeq")]
    public int? PreviousSeq { get; init; }
    [JsonPropertyName("newSeq")]
    public int NewSeq { get; init; }
}
