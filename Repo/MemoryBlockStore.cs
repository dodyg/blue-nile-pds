using System.Text.Json;
using CID;
using Common;
using PeterO.Cbor;

namespace Repo;

public class MemoryBlockStore : IRepoStorage
{
    private BlockMap _blocks;
    private Cid? _root = null;
    private string? _rev = null;

    public MemoryBlockStore(BlockMap? blocks)
    {
        _blocks = new BlockMap();
        if (blocks != null)
        {
            _blocks.AddMap(blocks);
        }
    }
    
    public Task<Cid?> GetRoot()
    {
        return Task.FromResult(_root);
    }
    
    public Task PutBlock(Cid cid, byte[] block, string rev)
    {
        _blocks.Set(cid, block);
        _rev = rev;
        return Task.CompletedTask;
    }
    
    public Task PutMany(BlockMap blocks, string rev)
    {
        _blocks.AddMap(blocks);
        _rev = rev;
        return Task.CompletedTask;
    }
    
    public Task UpdateRoot(Cid cid, string rev)
    {
        _root = cid;
        _rev = rev;
        return Task.CompletedTask;
    }
    
    public Task ApplyCommit(CommitData commit)
    {
        _root = commit.Cid;
        _rev = commit.Rev;
        foreach (var (cid, block) in commit.NewBlocks.Iterator)
        {
            _blocks.Set(cid, block);
        }
        foreach (var cid in commit.RemovedCids.ToArray())
        {
            _blocks.Delete(cid);
        }
        
        return Task.CompletedTask;
    }
    
    public Task<byte[]> GetBytes(Cid cid)
    {
        if (!_blocks.Has(cid))
        {
            throw new Exception("Block not found");
        }
        return Task.FromResult(_blocks.Get(cid)!);
    }
    
    public Task<bool> Has(Cid cid)
    {
        return Task.FromResult(_blocks.Has(cid));
    }
    
    public Task<(BlockMap blocks, Cid[] missing)> GetBlocks(Cid[] cids)
    {
        var missing = cids.Where(c => !_blocks.Has(c)).ToArray();
        var blocks = new BlockMap();
        foreach (var cid in cids)
        {
            if (_blocks.Has(cid))
            {
                blocks.Set(cid, _blocks.Get(cid)!);
            }
        }
        return Task.FromResult((blocks, missing));
    }
    
    public async Task<(CBORObject obj, byte[] bytes)> ReadObjAndBytes(Cid cid)
    {
        var result = await AttemptRead(cid);
        if (result == null)
        {
            throw new Exception("Block not found");
        }
        
        return result.Value;
    }
    public async Task<(CBORObject obj, byte[] bytes)?> AttemptRead(Cid cid)
    {
        try
        {
            var bytes = await GetBytes(cid);
            var obj = CBORObject.DecodeFromBytes(bytes);
            return (obj, bytes);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
