using BlueNilePds.Pds.ActorStore.Db;
using BlueNilePds.Pds.BlobStore;
using BlueNilePds.Pds.Config;
using BlueNilePds.Core.CID;
using Microsoft.EntityFrameworkCore;
using BlueNilePds.Core.Repo;

namespace BlueNilePds.Host.Services;

public class BlobGarbageCollectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BlobGarbageCollectionService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _tempBlobMaxAge;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BlobGarbageCollectionService(IServiceProvider serviceProvider, ILogger<BlobGarbageCollectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interval = TimeSpan.FromHours(1);
        _tempBlobMaxAge = TimeSpan.FromHours(24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Blob GC service started, interval: {Interval}, temp max age: {TempMaxAge}", _interval, _tempBlobMaxAge);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!await _lock.WaitAsync(0, stoppingToken))
            {
                _logger.LogDebug("Blob GC already running, skipping this cycle");
                continue;
            }

            try
            {
                await RunGcAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Blob GC cycle failed");
            }
            finally
            {
                _lock.Release();
            }
        }

        _logger.LogInformation("Blob GC service stopped");
    }

    /// <summary>
    /// Runs a single GC cycle over every actor store. Public so it can be
    /// invoked from tests and operational tooling; the background loop in
    /// <see cref="ExecuteAsync"/> delegates to it.
    /// </summary>
    public async Task RunOnceAsync(CancellationToken stoppingToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var actorStoreConfig = scope.ServiceProvider.GetRequiredService<ActorStoreConfig>();
        var blobStoreFactory = scope.ServiceProvider.GetRequiredService<BlobStoreFactory>();

        if (!Directory.Exists(actorStoreConfig.Directory))
        {
            return;
        }

        var storeFiles = Directory
            .EnumerateFiles(actorStoreConfig.Directory, "store.sqlite", SearchOption.AllDirectories)
            .ToList();

        foreach (var storeFile in storeFiles)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                await RunGcForStoreAsync(storeFile, actorStoreConfig, blobStoreFactory, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Blob GC failed for actor store {Path}, continuing with next store", storeFile);
            }
        }
    }

    private async Task RunGcAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
    }

    private async Task RunGcForStoreAsync(
        string storeFile,
        ActorStoreConfig actorStoreConfig,
        BlobStoreFactory blobStoreFactory,
        CancellationToken stoppingToken)
    {
        var connectionString = $"Data Source={storeFile};";
        if (actorStoreConfig.DisableWalAutoCheckpoint)
        {
            connectionString += "wal_autocheckpoint=0;";
        }

        var options = new DbContextOptionsBuilder<ActorStoreDb>()
            .UseSqlite(connectionString)
            .Options;

        using var db = new ActorStoreDb(options);

        // The DID owns this store; resolve it from the store itself rather than
        // parsing the directory layout.
        var did = await db.RepoRoots.AsNoTracking()
            .Select(r => r.Did)
            .FirstOrDefaultAsync(stoppingToken);
        if (string.IsNullOrWhiteSpace(did))
        {
            _logger.LogDebug("Blob GC skipping actor store with no repo root: {Path}", storeFile);
            return;
        }

        var blobStore = blobStoreFactory.Create(did);

        // Temp blob GC: delete temp blobs older than 24h with no associated records
        var tempCutoff = DateTime.UtcNow - _tempBlobMaxAge;
        var orphanedTempBlobs = await db.Blobs
            .Where(b => b.Status == BlobStatus.Temporary && b.CreatedAt < tempCutoff)
            .Where(b => !db.RecordBlobs.Any(rb => rb.BlobCid == b.Cid))
            .ToListAsync(stoppingToken);

        foreach (var blob in orphanedTempBlobs)
        {
            try
            {
                await blobStore.DeleteAsync(Cid.FromString(blob.Cid));
                db.Blobs.Remove(blob);
                _logger.LogDebug("GC'd temp blob {Cid}", blob.Cid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to GC temp blob {Cid}", blob.Cid);
            }
        }

        if (orphanedTempBlobs.Count > 0)
        {
            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("GC'd {Count} temp blobs", orphanedTempBlobs.Count);
        }

        // Orphaned permanent blob GC: no record_blob references
        var orphanedPermanentBlobs = await db.Blobs
            .Where(b => b.Status == BlobStatus.Permanent)
            .Where(b => !db.RecordBlobs.Any(rb => rb.BlobCid == b.Cid))
            .ToListAsync(stoppingToken);

        foreach (var blob in orphanedPermanentBlobs)
        {
            try
            {
                await blobStore.DeleteAsync(Cid.FromString(blob.Cid));
                db.Blobs.Remove(blob);
                _logger.LogDebug("GC'd orphaned permanent blob {Cid}", blob.Cid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to GC orphaned permanent blob {Cid}", blob.Cid);
            }
        }

        if (orphanedPermanentBlobs.Count > 0)
        {
            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("GC'd {Count} orphaned permanent blobs", orphanedPermanentBlobs.Count);
        }
    }
}
