using ActorStore.Db;
using CarpaNet;
using CID;
using Crypto;
using Microsoft.EntityFrameworkCore;
using Repo;
using Xrpc;

namespace ActorStore.Repo;

// lol
public class RepoRepository
{
    private readonly ActorStoreDb _db;
    private readonly string _did;
    private readonly IKeyPair _keyPair;
    private readonly DateTime _now = DateTime.UtcNow;
    public RecordRepository Record { get; }
    public SqlRepoTransactor Storage { get; }
    public BlobTransactor Blob { get; }
    public RepoRepository(ActorStoreDb db, string did, IKeyPair keyPair, SqlRepoTransactor storage, RecordRepository record,
    IBlobStore blobStore)
    {
        _db = db;
        _did = did;
        _keyPair = keyPair;
        Storage = storage;
        Record = record;
        Blob = new BlobTransactor(blobStore, db);
    }

    public async Task<string[]> GetCollectionsAsync()
    {
        return await _db.Records
            .Select(x => x.Collection)
            .Distinct()
            .ToArrayAsync();
    }

    public async Task<CommitData> CreateRepoAsync(PreparedCreate[] writes)
    {
        var writeOpts = writes.Select(x => x.CreateWriteToOp()).ToArray();
        var commit = await global::Repo.Repo.FormatInitCommitAsync(Storage, _did, _keyPair, writeOpts);

        await Storage.ApplyCommitAsync(commit);
        await IndexWritesAsync(writes.Cast<IPreparedWrite>().ToArray(), commit.Rev);
        // await Blob.ProcessWriteBlobs(commit.Rev, writes);

        return commit;
    }

    public async Task IndexWritesAsync(IPreparedWrite[] writes, string rev)
    {
        foreach (var write in writes)
        {
            if (write is PreparedCreate create)
            {
                await Record.IndexRecordAsync(create.Uri, create.Cid, create.Record, WriteOpAction.Create, rev, _now);
            }
            else if (write is PreparedUpdate update)
            {
                await Record.IndexRecordAsync(update.Uri, update.Cid, update.Record, WriteOpAction.Update, rev, _now);
            }
            else if (write is PreparedDelete delete)
            {
                await Record.DeleteRecordAsync(delete.Uri);
            }
        }
    }

    public async Task<CommitData> ProcessWritesAsync(IPreparedWrite[] writes, Cid? swapCommitCid)
    {
        var commit = await FormatCommitAsync(writes, swapCommitCid);

        // T-11: validate blob constraints before applying
        await ValidateBlobConstraintsAsync(writes);

        await Storage.ApplyCommitAsync(commit);
        await IndexWritesAsync(writes, commit.Rev);
        await Blob.ProcessWriteBlobsAsync(commit.Rev, writes);
        return commit;
    }

    private async Task ValidateBlobConstraintsAsync(IPreparedWrite[] writes)
    {
        var dataWrites = writes
            .Where(w => w is PreparedCreate or PreparedUpdate)
            .Cast<IPreparedDataWrite>();

        foreach (var write in dataWrites)
        {
            foreach (var blobRef in write.Blobs)
            {
                var blob = await Blob.GetBlobAsync(blobRef.Cid);
                if (blob == null)
                {
                    continue; // existence check happens in ProcessWriteBlobsAsync
                }

                if (blobRef.Constraints.Accept?.Length > 0)
                {
                    var accepted = blobRef.Constraints.Accept.Any(a =>
                        blob.MimeType.Equals(a, StringComparison.OrdinalIgnoreCase) ||
                        (a.EndsWith("/*") && blob.MimeType.StartsWith(a[..^1], StringComparison.OrdinalIgnoreCase)));
                    if (!accepted)
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail(
                            $"Blob {blobRef.Cid} MIME type {blob.MimeType} is not accepted for this field. Accepted: {string.Join(", ", blobRef.Constraints.Accept)}"));
                    }
                }

                if (blobRef.Constraints.MaxSize.HasValue && blob.Size > blobRef.Constraints.MaxSize.Value)
                {
                    throw new XRPCError(new InvalidRequestErrorDetail(
                        $"Blob {blobRef.Cid} size {blob.Size} exceeds maximum {blobRef.Constraints.MaxSize.Value}"));
                }
            }
        }
    }

    public async Task<CommitData> FormatCommitAsync(IPreparedWrite[] writes, Cid? swapCommit)
    {
        var currRoot = await Storage.GetRootDetailedAsync();
        if (swapCommit != null && !currRoot.Cid.Equals(swapCommit))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Bad commit swap"));
        }

        await Storage.CacheRevAsync(currRoot.Rev);
        var newRecordsCids = new List<Cid>();
        var delAndUpdateUris = new List<ATUri>();
        foreach (var write in writes)
        {
            Cid? swapCid = null;
            if (write is PreparedCreate create)
            {
                newRecordsCids.Add(create.Cid);
                swapCid = create.SwapCid;
            }
            else if (write is PreparedUpdate update)
            {
                newRecordsCids.Add(update.Cid);
                swapCid = update.SwapCid;
            }
            else if (write is PreparedDelete delete)
            {
                delAndUpdateUris.Add(delete.Uri);
                swapCid = delete.SwapCid;
            }

            if (swapCid == null)
            {
                continue;
            }

            var record = await Record.GetRecordAsync(write.Uri, null, true);
            Cid? currRecord = record != null ? Cid.FromString(record.Cid) : null;
            if (write.Action == WriteOpAction.Create && swapCid != null)
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Cannot swap on create"));
            }
            if (write.Action == WriteOpAction.Update && swapCid == null)
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Must swap on update"));
            }
            if (write.Action == WriteOpAction.Delete && swapCid == null)
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Must swap on delete"));
            }
            if ((currRecord != null || swapCid != null) && !currRecord?.Equals(swapCid) == true)
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Bad swap"));
            }
        }

        var repo = await global::Repo.Repo.LoadAsync(Storage, currRoot.Cid);
        var writeOps = writes.Select(WriteToOp).ToArray();
        var commit = await repo.FormatCommitAsync(writeOps, _keyPair);

        // find blocks that would be deleted but are referenced by another record
        var dupeRecordCids = await GetDuplicateRecordCidsAsync(commit.RemovedCids.ToArray(), delAndUpdateUris.ToArray());
        foreach (var dupeCid in dupeRecordCids)
        {
            commit.RemovedCids.Delete(dupeCid);
        }

        var newRecordBlocks = commit.NewBlocks.GetMany(newRecordsCids.ToArray());
        if (newRecordBlocks.missing.Length > 0)
        {
            var missingBlocks = await Storage.GetBlocksAsync(newRecordBlocks.missing);
            commit.NewBlocks.AddMap(missingBlocks.blocks);
        }

        // T-05: Repo write size limit (2MB)
        if (commit.NewBlocks.ByteSize > 2 * 1024 * 1024)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Commit too large"));
        }

        return commit;
    }

    private async Task<Cid[]> GetDuplicateRecordCidsAsync(Cid[] cids, ATUri[] touchedUris)
    {
        if (cids.Length == 0 || touchedUris.Length == 0)
        {
            return [];
        }
        var cidStrs = cids.Select(x => x.ToString()).ToArray();
        var uriStrs = touchedUris.Select(x => x.ToString()).ToArray();

        var res = await _db.Records
            .Where(x => cidStrs.Contains(x.Cid) && !uriStrs.Contains(x.Uri))
            .Select(x => x.Cid)
            .ToArrayAsync();

        return res.Select(Cid.FromString).ToArray();
    }

    private static IRecordWriteOp WriteToOp(IPreparedWrite preparedWrite)
    {
        return preparedWrite switch
        {
            PreparedCreate create => create.CreateWriteToOp(),
            PreparedUpdate update => update.UpdateWriteToOp(),
            PreparedDelete delete => delete.DeleteWriteToOp(),
            _ => throw new XRPCError(new InvalidRequestErrorDetail("Invalid write type"))
        };
    }
}
