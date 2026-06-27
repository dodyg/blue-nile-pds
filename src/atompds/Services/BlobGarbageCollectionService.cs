using ActorStore.Db;
using ActorStore.Repo;
using CID;
using Microsoft.EntityFrameworkCore;
using Repo;

namespace atompds.Services;

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

    private async Task RunGcAsync(CancellationToken stoppingToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ActorStoreDb>();
        var blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();

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
