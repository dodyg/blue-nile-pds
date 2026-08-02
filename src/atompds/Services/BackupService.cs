using System.Text.Json;
using System.Text.Json.Serialization;
using Config;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Xrpc;

namespace atompds.Services;

public enum BackupStatus
{
    Idle,
    Running,
    Completed,
    Failed
}

public class BackupJobState
{
    public BackupStatus Status { get; set; } = BackupStatus.Idle;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FileName { get; set; }
    public long? SizeBytes { get; set; }
    public string? Error { get; set; }
}

public class BackupEntry
{
    public required string FileName { get; init; }
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}

public class BackupService
{
    private readonly BackupConfig _config;
    private readonly DatabaseConfig _dbConfig;
    private readonly ActorStoreConfig _actorStoreConfig;
    private readonly BlobStoreConfig _blobstoreConfig;
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly ILogger<BackupService> _logger;
    private readonly object _stateLock = new();
    private BackupJobState _state = new();
    private bool _stagingCleanupDone;

    public BackupService(
        BackupConfig config,
        DatabaseConfig dbConfig,
        ActorStoreConfig actorStoreConfig,
        BlobStoreConfig blobstoreConfig,
        IBackgroundJobQueue jobQueue,
        ILogger<BackupService> logger)
    {
        _config = config;
        _dbConfig = dbConfig;
        _actorStoreConfig = actorStoreConfig;
        _blobstoreConfig = blobstoreConfig;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public BackupJobState GetStatus()
    {
        lock (_stateLock)
        {
            return new BackupJobState
            {
                Status = _state.Status,
                StartedAt = _state.StartedAt,
                CompletedAt = _state.CompletedAt,
                FileName = _state.FileName,
                SizeBytes = _state.SizeBytes,
                Error = _state.Error
            };
        }
    }

    public List<BackupEntry> ListBackups()
    {
        if (!Directory.Exists(_config.Directory))
            return [];

        return Directory.EnumerateFiles(_config.Directory, "backup-*.zip")
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new BackupEntry
                {
                    FileName = info.Name,
                    CreatedAt = info.CreationTimeUtc,
                    SizeBytes = info.Length
                };
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public string GetBackupPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            Path.GetFileName(fileName) != fileName ||
            !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid backup file name"));
        }

        var fullPath = Path.Combine(_config.Directory, fileName);
        if (!File.Exists(fullPath))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Backup not found"));
        }

        return fullPath;
    }

    public void DeleteBackup(string fileName)
    {
        var path = GetBackupPath(fileName);
        File.Delete(path);
    }

    public async ValueTask CreateBackupAsync()
    {
        lock (_stateLock)
        {
            if (_state.Status == BackupStatus.Running)
            {
                throw new XRPCError(new ErrorDetail("BackupAlreadyRunning", "A backup is already in progress"));
            }

            _state = new BackupJobState
            {
                Status = BackupStatus.Running,
                StartedAt = DateTime.UtcNow
            };
        }

        await _jobQueue.EnqueueAsync(async _ =>
        {
            await Task.Yield();
            RunBackup();
        });

        _logger.LogInformation("Backup job enqueued");
    }

    private void RunBackup()
    {
        if (!_stagingCleanupDone)
        {
            CleanupStaleStagingDirs();
            _stagingCleanupDone = true;
        }

        var stagingDir = Path.Combine(_config.Directory, "staging");
        var fileName = $"backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        var zipPath = Path.Combine(_config.Directory, fileName);

        try
        {
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, true);
            }
            Directory.CreateDirectory(stagingDir);

            var manifest = new Dictionary<string, object?>
            {
                ["createdAt"] = DateTime.UtcNow.ToString("O"),
                ["backupFile"] = fileName
            };

            SnapshotDatabase(_dbConfig.AccountDbLoc, Path.Combine(stagingDir, "account.sqlite"), "account");
            SnapshotDatabase(_dbConfig.SequencerDbLoc, Path.Combine(stagingDir, "sequencer.sqlite"), "sequencer");
            SnapshotDatabase(_dbConfig.DidCacheDbLoc, Path.Combine(stagingDir, "did_cache.sqlite"), "did_cache");

            var (actorCount, actorDbCount) = SnapshotActorStores(stagingDir);
            manifest["actorCount"] = actorCount;

            var blobsIncluded = SnapshotBlobstore(stagingDir, manifest);

            WriteManifest(stagingDir, manifest);

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            System.IO.Compression.ZipFile.CreateFromDirectory(stagingDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);

            var size = new FileInfo(zipPath).Length;
            _logger.LogInformation("Backup completed: {FileName} ({Size} bytes, {Actors} actors, blobs included: {Blobs})", fileName, size, actorCount, blobsIncluded);

            lock (_stateLock)
            {
                _state.Status = BackupStatus.Completed;
                _state.CompletedAt = DateTime.UtcNow;
                _state.FileName = fileName;
                _state.SizeBytes = size;
                _state.Error = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            try
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up partial backup zip");
            }

            lock (_stateLock)
            {
                _state.Status = BackupStatus.Failed;
                _state.CompletedAt = DateTime.UtcNow;
                _state.Error = ex.Message;
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up staging directory");
            }

            PruneOldBackups();
        }
    }

    private void SnapshotDatabase(string sourcePath, string targetPath, string name)
    {
        if (!File.Exists(sourcePath))
        {
            _logger.LogWarning("Backup: database {Name} not found at {Path}; skipping", name, sourcePath);
            return;
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        using var connection = new SqliteConnection($"Data Source={sourcePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"VACUUM INTO '{EscapeSqliteString(targetPath)}'";
        command.ExecuteNonQuery();
        _logger.LogDebug("Backup: snapshotted {Name} database", name);
    }

    private static string EscapeSqliteString(string value)
    {
        return value.Replace("'", "''");
    }

    private (int ActorCount, int ActorDbCount) SnapshotActorStores(string stagingDir)
    {
        if (!Directory.Exists(_actorStoreConfig.Directory))
        {
            return (0, 0);
        }

        var actorCount = 0;
        var actorDbCount = 0;
        var actorsRoot = Path.Combine(stagingDir, "actors");

        foreach (var hashDir in Directory.EnumerateDirectories(_actorStoreConfig.Directory))
        {
            foreach (var actorDir in Directory.EnumerateDirectories(hashDir))
            {
                actorCount++;
                var dbPath = Path.Combine(actorDir, "store.sqlite");
                var keyPath = Path.Combine(actorDir, "key");
                if (!File.Exists(dbPath))
                    continue;

                var relativePath = Path.GetRelativePath(_actorStoreConfig.Directory, actorDir);
                var targetDir = Path.Combine(actorsRoot, relativePath);
                Directory.CreateDirectory(targetDir);

                SnapshotDatabase(dbPath, Path.Combine(targetDir, "store.sqlite"), $"actor {relativePath}");
                actorDbCount++;

                if (File.Exists(keyPath))
                {
                    File.Copy(keyPath, Path.Combine(targetDir, "key"), overwrite: true);
                }
            }
        }

        return (actorCount, actorDbCount);
    }

    private bool SnapshotBlobstore(string stagingDir, Dictionary<string, object?> manifest)
    {
        if (_blobstoreConfig is not DiskBlobstoreConfig diskConfig)
        {
            var provider = _blobstoreConfig is S3BlobstoreConfig ? "s3" : "unknown";
            _logger.LogWarning("Backup: {Provider} blobstore configured; blob files are not included in the backup", provider);
            manifest["blobsIncluded"] = false;
            manifest["blobstoreProvider"] = provider;
            return false;
        }

        var location = ExpandPath(diskConfig.Location);
        manifest["blobsIncluded"] = true;
        manifest["blobstoreProvider"] = "disk";

        if (!Directory.Exists(location))
        {
            _logger.LogWarning("Backup: disk blobstore not found at {Path}; skipping blobs", location);
            manifest["blobsIncluded"] = false;
            return false;
        }

        var blobsRoot = Path.Combine(stagingDir, "blocks");
        Directory.CreateDirectory(blobsRoot);
        CopyDirectory(location, blobsRoot);
        return true;
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var relative = Path.GetFileName(file);
            File.Copy(file, Path.Combine(targetDir, relative), overwrite: true);
        }

        foreach (var subDir in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(subDir);
            CopyDirectory(subDir, Path.Combine(targetDir, name));
        }
    }

    private static void WriteManifest(string stagingDir, Dictionary<string, object?> manifest)
    {
        var manifestPath = Path.Combine(stagingDir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        File.WriteAllText(manifestPath, json);
    }

    private void CleanupStaleStagingDirs()
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(_config.Directory, "staging*"))
            {
                Directory.Delete(dir, true);
                _logger.LogInformation("Backup: cleaned up stale staging directory {Dir}", dir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup: failed to clean up stale staging directories");
        }
    }

    private void PruneOldBackups()
    {
        try
        {
            var backups = ListBackups();
            foreach (var entry in backups.Skip(Math.Max(0, _config.MaxKeep)))
            {
                var path = Path.Combine(_config.Directory, entry.FileName);
                File.Delete(path);
                _logger.LogInformation("Backup: pruned old backup {FileName}", entry.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup: failed to prune old backups");
        }
    }

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (path.StartsWith("~/"))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);

        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return path;
    }
}
