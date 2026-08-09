using ActorStore;
using ActorStore.Repo;
using CID;
using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PeterO.Cbor;
using Repo;
using Repo.MST;
using Xrpc;

namespace atompds.Services;

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

    private async Task RunResyncAsync(string did, IServiceProvider sp)
    {
        try
        {
            var provider = sp.GetRequiredService<ActorRepositoryProvider>();
            var accountRepository = sp.GetRequiredService<AccountManager.AccountRepository>();

            if (!provider.Exists(did))
            {
                throw new XRPCError(new InvalidRequestErrorDetail($"Repo not found for DID {did}"));
            }

            await using var actorStore = provider.Open(did);

            var (oldRootCid, _) = await actorStore.Repo.Storage.GetRootDetailedAsync();
            var oldRepo = await global::Repo.Repo.LoadAsync(actorStore.Repo.Storage, oldRootCid);

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
                var (collection, rkey) = Repo.MST.Util.ParseDataKey(leaf.Key);
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

            var keyPair = provider.KeyPair(did);
            var commit = await global::Repo.Repo.FormatInitCommitAsync(actorStore.Repo.Storage, did, keyPair, ops.ToArray());

            Cid[] toDelete = [];
            await actorStore.TransactRepoAsync(async repoRef =>
            {
                await repoRef.Repo.Storage.ApplyCommitAsync(commit);
                await repoRef.Record.ReindexResyncAsync(commit.Rev, ops);

                var newRepo = await global::Repo.Repo.LoadAsync(repoRef.Repo.Storage, commit.Cid);
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

                toDelete = oldReachable.Where(c => !newReachable.Contains(c)).ToArray();
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
