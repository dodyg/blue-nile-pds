using CID;
using Common;
using PeterO.Cbor;
using Repo.MST;

namespace Repo;

public class BlockMap
{
    private Dictionary<string, byte[]> _map = new();
    
    public IEnumerable<(Cid, byte[])> Iterator => _map.Select(kv => (Cid.FromString(kv.Key), kv.Value));
    public Entry[] Entries => _map.Select(kv => new Entry(Cid.FromString(kv.Key), kv.Value)).ToArray();
    public Cid[] Cids => _map.Keys.Select(Cid.FromString).ToArray();
    public bool Has(Cid cid) => _map.ContainsKey(cid.ToString());
    public byte[]? Get(Cid cid) => _map.GetValueOrDefault(cid.ToString());
    public void Delete(Cid cid) => _map.Remove(cid.ToString());
    public void Clear() => _map.Clear();
    public int Size => _map.Count;
    public ulong ByteSize => (ulong) _map.Values.Sum(b => (long) b.Length);

    public Cid Add(CBORObject data)
    {
        var block = CborBlock.Encode(data);
        Set(block.Cid, block.Bytes);
        return block.Cid;
    }
    
    public void AddMap(BlockMap toAdd)
    {
        foreach (var (cid, block) in toAdd.Iterator)
        {
            Set(cid, block);
        }
    }
    
    public void Set(Cid cid, byte[] block)
    {
        _map[cid.ToString()] = block;
    }

    public (BlockMap blocks, Cid[] missing) GetMany(Cid[] cids)
    {
        var missing = new List<Cid>();
        var blocks = new BlockMap();
        foreach (var cid in cids)
        {
            var got = Get(cid);
            if (got != null)
            {
                blocks.Set(cid, got);
            }
            else
            {
                missing.Add(cid);
            }
        }
        return (blocks, missing.ToArray());
    }
}