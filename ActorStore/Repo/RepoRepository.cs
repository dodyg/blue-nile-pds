using ActorStore.Db;
using CID;
using Crypto;
using Microsoft.EntityFrameworkCore;
using PeterO.Cbor;
using Repo;
using Repo.MST;

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
        await using var tx = await _db.Database.BeginTransactionAsync();
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
}

public class SqlRepoTransactor : IRepoStorage
{
    private readonly ActorStoreDb _db;
    private readonly string _did;
    private BlockMap _cache = new();
    private string now;

    public SqlRepoTransactor(ActorStoreDb db, string did, string? now)
    {
        _db = db;
        _did = did;
        this.now = now ?? DateTime.UtcNow.ToString("O");
    }
    
    public async Task<CID.Cid?> GetRoot()
    {
        var detailedRoot = await GetRootDetailed();
        return detailedRoot.Cid;
    }

    public async Task<(CID.Cid Cid, string Rev)> GetRootDetailed()
    {
        var res = await _db.RepoRoots.SingleAsync();
        return (Cid.FromString(res.Cid), res.Rev);
    }
    
    public async Task PutBlock(Cid cid, byte[] block, string rev)
    {
        var newBlock = new RepoBlock
        {
            Cid = cid.ToString(),
            Content = block,
            RepoRev = rev,
            Size = block.Length
        };
        
        // TODO: should find a way to do "ON CONFLICT DO NOTHING" here
        if (_db.RepoBlocks.Any(x => x.Cid == cid.ToString()))
        {
            _db.RepoBlocks.Update(newBlock);
        }
        else
        {
            _db.RepoBlocks.Add(newBlock);
        }
        
        await _db.SaveChangesAsync();
        _cache.Set(cid, block);
    }
    public Task PutMany(BlockMap toPut, string rev)
    {
        var blocks = new List<RepoBlock>();
        foreach (var (cid, block) in toPut.Iterator)
        {
            blocks.Add(new RepoBlock
            {
                Cid = cid.ToString(),
                Content = block,
                RepoRev = rev,
                Size = block.Length
            });
        }
        
        // TODO: should find a way to do "ON CONFLICT DO NOTHING" here
        foreach (var block in blocks)
        {
            if (_db.RepoBlocks.Any(x => x.Cid == block.Cid))
            {
                _db.RepoBlocks.Update(block);
            }
            else
            {
                _db.RepoBlocks.Add(block);
            }
        }
        
        return _db.SaveChangesAsync();
    }
    public Task UpdateRoot(Cid cid, string rev)
    {
        var newRoot = new RepoRoot
        {
            Cid = cid.ToString(),
            Rev = rev,
            Did = _did,
            IndexedAt = DateTime.UtcNow
        };
        
        if (_db.RepoRoots.Any(x => x.Did == _did))
        {
            _db.RepoRoots.Update(newRoot);
        }
        else
        {
            _db.RepoRoots.Add(newRoot);
        }
        
        return _db.SaveChangesAsync();
    }
    public async Task ApplyCommit(CommitData commit)
    {
        await UpdateRoot(commit.Cid, commit.Rev);
        await PutMany(commit.NewBlocks, commit.Rev);
        await DeleteMany(commit.RemovedCids.ToArray());
    }
    
    public async Task DeleteMany(Cid[] cids)
    {
        foreach (var cid in cids)
        {
            var cidStr = cid.ToString();
            var block = await _db.RepoBlocks.Where(x => x.Cid == cidStr)
                .FirstOrDefaultAsync();
            if (block == null) continue;
            _db.RepoBlocks.Remove(block);
        }
        
        await _db.SaveChangesAsync();
    }
    
    public async Task<byte[]?> GetBytes(Cid cid)
    {
        var cached = _cache.Get(cid);
        if (cached != null)
        {
            return cached;
        }
        
        var cidStr = cid.ToString();
        var res = await _db.RepoBlocks.Where(x => x.Cid == cidStr)
            .Select(x => x.Content)
            .FirstOrDefaultAsync();
        
        if (res == null) return null;
        _cache.Set(cid, res);
        return res;
    }
    public async Task<bool> Has(Cid cid)
    {
        return (await GetBytes(cid)) != null;
    }
    public async Task<(BlockMap blocks, Cid[] missing)> GetBlocks(Cid[] cids)
    {
        var cached = _cache.GetMany(cids);
        if (cached.missing.Length < 1) return cached;
        var missing = new CidSet(cached.missing);
        var missingStr = cached.missing.Select(x => x.ToString()).ToArray();
        var blocks = new BlockMap();
        // TODO: This should be chunked
        foreach (var missingCid in missingStr)
        {
            var res = await _db.RepoBlocks.Where(x => x.Cid == missingCid)
                .Select(x => new {x.Cid, x.Content})
                .FirstOrDefaultAsync();
            if (res == null) continue;
            blocks.Set(Cid.FromString(res.Cid), res.Content);
            missing.Delete(Cid.FromString(res.Cid));
        }
        
        _cache.AddMap(blocks);
        blocks.AddMap(cached.blocks);
        return (blocks, missing.ToArray());
    }
    public async Task<(CBORObject obj, byte[] bytes)> ReadObjAndBytes(Cid cid)
    {
        var bytes = await GetBytes(cid);
        if (bytes == null) throw new Exception("Block not found");
        var obj = CBORObject.DecodeFromBytes(bytes);
        return (obj, bytes);
    }
    public async Task<(CBORObject obj, byte[] bytes)?> AttemptRead(Cid cid)
    {
        try
        {
            return await ReadObjAndBytes(cid);
        }
        catch (Exception)
        {
            return null;
        }
    }
}