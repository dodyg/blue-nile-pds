using ActorStore.Db;
using CID;
using Crypto;
using FishyFlip.Models;
using Microsoft.EntityFrameworkCore;
using Repo;

namespace ActorStore.Repo;

// lol
public class RepoRepository
{
    private readonly ActorStoreDb _db;
    private readonly string _did;
    private readonly IKeyPair _keyPair;
    private readonly SqlRepoTransactor _storage;
    private readonly RecordRepository _record;
    private readonly DateTime _now = DateTime.UtcNow;
    public RepoRepository(ActorStoreDb db, string did, IKeyPair keyPair, SqlRepoTransactor storage, RecordRepository record)
    {
        _db = db;
        _did = did;
        _keyPair = keyPair;
        _storage = storage;
        _record = record;
    }

    public async Task<CommitData> CreateRepo(PreparedCreate[] writes)
    {
        var writeOpts = writes.Select(x => x.CreateWriteToOp()).ToArray();
        var commit = await global::Repo.Repo.FormatInitCommit(_storage, _did, _keyPair, writeOpts);
        
        await _storage.ApplyCommit(commit);
        await IndexWrites(writes.Cast<IPreparedWrite>().ToArray(), commit.Rev);
        // TODO: Actually do stuff with blobs
        
        return commit;
    }

    public async Task IndexWrites(IPreparedWrite[] writes, string rev)
    {
        foreach (var write in writes)
        {
            if (write is PreparedCreate create)
            {
                await _record.IndexRecord(create.Uri, create.Cid, create.Record, WriteOpAction.Create, rev, _now);
            }
            else if (write is PreparedUpdate update)
            {
                await _record.IndexRecord(update.Uri, update.Cid, update.Record, WriteOpAction.Update, rev, _now);
            }
            else if (write is PreparedDelete delete)
            {
                await _record.DeleteRecord(delete.Uri);
            }
        }
    }

    public async Task<CommitData> ProcessWrites(IPreparedWrite[] writes, Cid? swapCommitCid)
    {
        var commit = await FormatCommit(writes, swapCommitCid);
        await _storage.ApplyCommit(commit);
        await IndexWrites(writes, commit.Rev);
        // TODO: blob.processWriteBlobs.
        return commit;
    }

    public async Task<CommitData> FormatCommit(IPreparedWrite[] writes, Cid? swapCommit)
    {
        var currRoot = await _storage.GetRootDetailed();
        if (swapCommit != null && !currRoot.Cid.Equals(swapCommit))
        {
            throw new Exception("Bad commit swap");
        }
        
        await _storage.CacheRev(currRoot.Rev);
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
            
            if (swapCid == null) continue;

            var record = await _record.GetRecord(write.Uri, null, true);
            Cid? currRecord = record != null ? Cid.FromString(record.Cid) : null;
            if (write.Action == WriteOpAction.Create && swapCid != null)
            {
                throw new Exception("Cannot swap on create");
            }
            if (write.Action == WriteOpAction.Update && swapCid == null)
            {
                throw new Exception("Must swap on update");
            }
            if (write.Action == WriteOpAction.Delete && swapCid == null)
            {
                throw new Exception("Must swap on delete");
            }
            if ((currRecord != null || swapCid != null) && !currRecord?.Equals(swapCid) == true)
            {
                throw new Exception("Bad swap");
            }
        }
        
        var repo = await global::Repo.Repo.Load(_storage, currRoot.Cid);
        var writeOps = writes.Select(WriteToOp).ToArray();
        var commit = await repo.FormatCommit(writeOps, _keyPair);
        
        // find blocks that would be deleted but are referenced by another record
        var dupeRecordCids = await GetDuplicateRecordCids(commit.RemovedCids.ToArray(), delAndUpdateUris.ToArray());
        foreach (var dupeCid in dupeRecordCids)
        {
            commit.RemovedCids.Delete(dupeCid);
        }

        var newRecordBlocks = commit.NewBlocks.GetMany(newRecordsCids.ToArray());
        if (newRecordBlocks.missing.Length > 0)
        {
            var missingBlocks = await _storage.GetBlocks(newRecordBlocks.missing);
            commit.NewBlocks.AddMap(missingBlocks.blocks);
        }

        return commit;
    }

    private async Task<Cid[]> GetDuplicateRecordCids(Cid[] cids, ATUri[] touchedUris)
    {
        if (cids.Length == 0 || touchedUris.Length == 0) return [];
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
            _ => throw new Exception("Invalid write type")
        };
    }
}