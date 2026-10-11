using BlueNilePds.Pds.ActorStore;
using BlueNilePds.Pds.ActorStore.Db;
using BlueNilePds.Pds.ActorStore.Repo;
using BlueNilePds.Core.CID;
using BlueNilePds.Core.Common;
using CarpaNet;
using Microsoft.EntityFrameworkCore;
using PeterO.Cbor;
using BlueNilePds.Core.Repo;
using BlueNilePds.Core.Repo.MST;
using BlueNilePds.Pds.Xrpc;
using BlueNilePds.Pds.AccountManager;

namespace BlueNilePds.Host.Services;

public enum RepoResyncStatus
{
    Idle,
    Running,
    Completed,
    Failed
}

public class RepoResyncJobState
{
    public RepoResyncStatus Status { get; set; } = RepoResyncStatus.Idle;
    public string? Did { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RecordsScanned { get; set; }
    public int RecordsRewritten { get; set; }
    public List<string> SkippedRecords { get; set; } = new();
    public string? Error { get; set; }
}

/// <summary>
/// Per-DID repo repair tool: re-encodes every record block in canonical DAG-CBOR and
/// rebuilds the repo from scratch. This rewrites the repo history for the target DID
/// (the new commit has no prev and a fresh rev), so it should only be used when stored
/// record blocks have diverged from their record-index Cids.
/// </summary>
public class RepoResyncService
{
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly ILogger<RepoResyncService> _logger;
    private readonly object _stateLock = new();
    private RepoResyncJobState _state = new();

    public RepoResyncService(IBackgroundJobQueue jobQueue, ILogger<RepoResyncService> logger)
    {
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public RepoResyncJobState GetStatus()
    {
        lock (_stateLock)
        {
            return new RepoResyncJobState
            {
                Status = _state.Status,
                Did = _state.Did,
                StartedAt = _state.StartedAt,
                CompletedAt = _state.CompletedAt,
                RecordsScanned = _state.RecordsScanned,
                RecordsRewritten = _state.RecordsRewritten,
                SkippedRecords = [.. _state.SkippedRecords],
                Error = _state.Error
            };
        }
    }

    public async ValueTask StartAsync(string did)
    {
        lock (_stateLock)
        {
            if (_state.Status == RepoResyncStatus.Running)
            {
                throw new XRPCError(new ErrorDetail("RepoResyncAlreadyRunning", "A repo resync is already in progress"));
            }

            _state = new RepoResyncJobState
            {
                Status = RepoResyncStatus.Running,
                Did = did,
                StartedAt = DateTime.UtcNow
            };
        }

        await _jobQueue.EnqueueAsync(async sp =>
        {
            await Task.Yield();
            await RunResyncAsync(did, sp);
        });

        _logger.LogInformation("Repo resync job enqueued for {Did}", did);
    }

    /// <summary>
    /// Rebuild record_blob from the re-encoded records. Resync historically never
    /// touched blob state, leaving the index drifted (live blobs looking orphaned to
    /// GC, or dead blobs pinned). Only the index rows and Temporary→Permanent flags
    /// are reconciled here; blob files are never moved or deleted by resync.
    /// Must run inside the resync transaction.
    /// </summary>
    private async Task ReconcileBlobReferencesAsync(string did, List<RecordCreateOp> ops, ActorRepository repoRef)
    {
        var blobDb = repoRef.Repo.Blob.Db;

        var desiredLinks = new HashSet<(string RecordUri, string BlobCid)>();
        foreach (var op in ops)
        {
            var uri = ATUri.Create(did, op.Collection, op.RKey).ToString();
            foreach (var blobRef in Prepare.ExtractBlobReferences(op.Record.ToJSONString()))
            {
                desiredLinks.Add((uri, blobRef.Cid.ToString()));
            }
        }

        var didPrefix = $"at://{did}/";
        var existingLinks = await blobDb.RecordBlobs
            .Where(rb => rb.RecordUri.StartsWith(didPrefix))
            .ToListAsync();

        var existingSet = existingLinks
            .Select(rb => (rb.RecordUri, rb.BlobCid))
            .ToHashSet();

        var linksToRemove = existingLinks
            .Where(rb => !desiredLinks.Contains((rb.RecordUri, rb.BlobCid)))
            .ToList();
        var linksToAdd = desiredLinks
            .Except(existingSet)
            .Select(x => new RecordBlob { RecordUri = x.RecordUri, BlobCid = x.BlobCid })
            .ToArray();

        if (linksToRemove.Count > 0)
        {
            blobDb.RecordBlobs.RemoveRange(linksToRemove);
        }
        if (linksToAdd.Length > 0)
        {
            await blobDb.RecordBlobs.AddRangeAsync(linksToAdd);
        }
        await blobDb.SaveChangesAsync();

        _logger.LogInformation(
            "Repo resync blob reconciliation for {Did}: {Added} links added, {Removed} stale links removed, {Total} desired",
            did, linksToAdd.Length, linksToRemove.Count, desiredLinks.Count);

        // Promote Temporary→Permanent where the stored file actually exists.
        // Temp keys from long ago are gone, so never call MakePermanentAsync here
        // (it throws when neither temp nor stored file exists).
        var desiredCids = desiredLinks.Select(x => x.BlobCid).Distinct().ToHashSet();
        var tempBlobs = await blobDb.Blobs
            .Where(b => desiredCids.Contains(b.Cid) && b.Status == BlobStatus.Temporary)
            .ToListAsync();
        foreach (var tempBlob in tempBlobs)
        {
            try
            {
                await using var _ = await repoRef.Repo.Blob.BlobStore.GetStreamAsync(Cid.FromString(tempBlob.Cid));
                tempBlob.Status = BlobStatus.Permanent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Repo resync for {Did}: blob file missing for {Cid}, leaving Temporary", did, tempBlob.Cid);
            }
        }
        await blobDb.SaveChangesAsync();

        var knownBlobCids = await blobDb.Blobs
            .Where(b => desiredCids.Contains(b.Cid))
            .Select(b => b.Cid)
            .ToHashSetAsync();
        foreach (var unknown in desiredCids.Except(knownBlobCids))
        {
            _logger.LogWarning("Repo resync for {Did}: record references blob {Cid} with no blob row; re-upload required", did, unknown);
        }
    }

    private async Task RunResyncAsync(string did, IServiceProvider sp)
    {
        try
        {
            var provider = sp.GetRequiredService<ActorRepositoryProvider>();
            var accountRepository = sp.GetRequiredService<AccountRepository>();

            if (!provider.Exists(did))
            {
                throw new XRPCError(new InvalidRequestErrorDetail($"Repo not found for DID {did}"));
            }

            await using var actorStore = provider.Open(did);

            var (oldRootCid, _) = await actorStore.Repo.Storage.GetRootDetailedAsync();
            var oldRepo = await Repo.LoadAsync(actorStore.Repo.Storage, oldRootCid);

            // collect CIDs reachable from the old root so unreferenced blocks can be GC'd afterwards
            var oldReachable = new HashSet<Cid> { oldRootCid };
            await foreach (var node in oldRepo.Data.WalkReachableAsync())
            {
                if (node is MST mst)
                {
                    oldReachable.Add(mst.Pointer);
                }
                else if (node is Leaf leaf)
                {
                    oldReachable.Add(leaf.Value);
                }
            }

            var ops = new List<RecordCreateOp>();
            var rewritten = 0;
            await foreach (var leaf in oldRepo.Data.ReachableLeavesAsync())
            {
                var (collection, rkey) = Core.Repo.MST.Util.ParseDataKey(leaf.Key);
                var bytes = await actorStore.Repo.Storage.GetBytesAsync(leaf.Value);
                if (bytes == null)
                {
                    throw new InvalidOperationException($"Missing block for record {leaf.Key} ({leaf.Value})");
                }

                var record = CBORObject.DecodeFromBytes(bytes);
                var canonicalBytes = CanonicalDagCbor.Encode(record);
                var canonicalCid = Prepare.CidForBytes(canonicalBytes);
                if (!canonicalBytes.AsSpan().SequenceEqual(bytes) || !canonicalCid.Equals(leaf.Value))
                {
                    rewritten++;
                }

                ops.Add(new RecordCreateOp(collection, rkey, record, canonicalCid, canonicalBytes));
            }

            // The MST walk above silently skips subtrees with missing blocks, so on a
            // corrupted repo it under-collects (or collects nothing). Fall back to the
            // record index, which survives corruption the tree does not.
            var indexedRecords = await actorStore.Record.GetAllRecordsAsync();
            var skipped = new List<string>();
            if (ops.Count < indexedRecords.Count)
            {
                var leafKeys = ops.Select(o => $"{o.Collection}/{o.RKey}").ToHashSet();
                foreach (var rec in indexedRecords)
                {
                    if (leafKeys.Contains($"{rec.Collection}/{rec.Rkey}"))
                    {
                        continue;
                    }

                    var recBytes = await actorStore.Repo.Storage.GetBytesAsync(Cid.FromString(rec.Cid));
                    if (recBytes == null)
                    {
                        skipped.Add(rec.Uri);
                        continue;
                    }

                    var recRecord = CBORObject.DecodeFromBytes(recBytes);
                    var recCanonicalBytes = CanonicalDagCbor.Encode(recRecord);
                    var recCanonicalCid = Prepare.CidForBytes(recCanonicalBytes);
                    if (!recCanonicalBytes.AsSpan().SequenceEqual(recBytes) || !recCanonicalCid.Equals(Cid.FromString(rec.Cid)))
                    {
                        rewritten++;
                    }

                    ops.Add(new RecordCreateOp(rec.Collection, rec.Rkey, recRecord, recCanonicalCid, recCanonicalBytes));
                }
            }

            if (skipped.Count > 0)
            {
                // Fail loudly: committing now would drop these records' last copies
                // (plus GC) with no trace. Operator must decide how to proceed.
                _logger.LogError(
                    "Repo resync refused for {Did}: {Skipped} indexed records have no readable block and would be lost: {Uris}",
                    did, skipped.Count, string.Join(", ", skipped.Take(20)));
                lock (_stateLock)
                {
                    _state.Status = RepoResyncStatus.Failed;
                    _state.CompletedAt = DateTime.UtcNow;
                    _state.RecordsScanned = ops.Count;
                    _state.RecordsRewritten = rewritten;
                    _state.SkippedRecords = skipped;
                    _state.Error = $"Refusing to resync: {skipped.Count} indexed records are unreadable and would be lost ({string.Join(", ", skipped.Take(5))}).";
                }
                return;
            }

            var keyPair = provider.KeyPair(did);
            var commit = await Repo.FormatInitCommitAsync(actorStore.Repo.Storage, did, keyPair, ops.ToArray());

            // CIDs still referenced by the record index must never be GC'd, even if
            // they are unreachable from the old root (corruption leftovers).
            var indexedCids = indexedRecords.Select(r => r.Cid).ToHashSet();

            Cid[] toDelete = [];
            await actorStore.TransactRepoAsync(async repoRef =>
            {
                await repoRef.Repo.Storage.ApplyCommitAsync(commit);
                await repoRef.Record.ReindexResyncAsync(commit.Rev, ops);
                await ReconcileBlobReferencesAsync(did, ops, repoRef);

                var newRepo = await Repo.LoadAsync(repoRef.Repo.Storage, commit.Cid);
                var newReachable = new HashSet<Cid> { commit.Cid };
                await foreach (var node in newRepo.Data.WalkReachableAsync())
                {
                    if (node is MST mst)
                    {
                        newReachable.Add(mst.Pointer);
                    }
                    else if (node is Leaf leaf)
                    {
                        newReachable.Add(leaf.Value);
                    }
                }

                toDelete = oldReachable
                    .Where(c => !newReachable.Contains(c) && !indexedCids.Contains(c.ToString()))
                    .ToArray();
                await repoRef.Repo.Storage.DeleteManyAsync(toDelete);
                return true;
            });

            await accountRepository.UpdateRepoRootAsync(did, commit.Cid, commit.Rev);

            _logger.LogWarning(
                "Repo resync completed for {Did}: rewrote {Rewritten}/{Scanned} records, new root {Root}, GC'd {Deleted} old blocks. " +
                "NOTE: repo history was rewritten (new commit has no prev).",
                did, rewritten, ops.Count, commit.Cid, toDelete.Length);

            lock (_stateLock)
            {
                _state.Status = RepoResyncStatus.Completed;
                _state.CompletedAt = DateTime.UtcNow;
                _state.RecordsScanned = ops.Count;
                _state.RecordsRewritten = rewritten;
                _state.SkippedRecords = skipped;
                _state.Error = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repo resync failed for {Did}", did);
            lock (_stateLock)
            {
                _state.Status = RepoResyncStatus.Failed;
                _state.CompletedAt = DateTime.UtcNow;
                _state.Error = ex.Message;
            }
        }
    }
}
