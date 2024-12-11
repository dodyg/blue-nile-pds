using System.Text;
using CID;
using Common;
using PeterO.Cbor;

namespace Repo.MST;

public record MST : INodeEntry
{
    public readonly IRepoStorage Storage;
    public Cid Pointer { get; private set; }
    private readonly INodeEntry[]? _entries;
    public int? Layer { get; private set; }
    public bool OutdatedPointer { get; private set; }
    
    public MST(IRepoStorage storage, Cid pointer, INodeEntry[]? entries = null, int? layer = null)
    {
        Storage = storage;
        Pointer = pointer;
        _entries = entries;
        Layer = layer;
    }
    
    public static MST Create(IRepoStorage storage, INodeEntry[] entries, MstOpts? opts = null)
    {
        var pointer = Util.CidForEntries(entries);
        return new MST(storage, pointer, entries, opts?.Layer);
    }
    
    public static MST FromData(IRepoStorage storage, NodeData data, MstOpts? opts = null)
    {
        var entries = Util.DeserializeNodeData(storage, data, opts);
        var pointer = CborBlock.Encode(data).Cid;
        return new MST(storage, pointer, entries, opts?.Layer);
    }

    public MST NewTree(INodeEntry[] entries)
    {
        return new MST(Storage, Util.CidForEntries(entries), entries, Layer)
        {
            OutdatedPointer = true
        };
    }

    public async Task<INodeEntry[]> GetEntries()
    {
        if (_entries != null)
        {
            return _entries;
        }
        
        var data = await Storage.ReadObjAndBytes(Pointer);
        var nodeData = NodeData.FromCborObject(data.obj);
        TreeEntry? firstLeaf = nodeData.Entries.Count > 0 ? nodeData.Entries[0] : null;
        int? layer = firstLeaf != null ? Util.LeadingZerosOnHash(firstLeaf.Value.Key) : null;
        var entries = Util.DeserializeNodeData(Storage, nodeData, layer == null ? null : new MstOpts(layer.Value));

        return entries;
    }

    public async Task<Cid> GetPointer()
    {
        if (!OutdatedPointer)
        {
            return Pointer;
        }
        
        var (cid, bytes) = await Serialize();
        Pointer = cid;
        OutdatedPointer = false;
        return Pointer;
    }

    public async Task<int> GetLayer()
    {
        Layer = await AttemptGetLayer() ?? 0;
        return Layer.Value;
    }
    
    public async Task<int?> AttemptGetLayer()
    {
        if (Layer != null)
        {
            return Layer.Value;
        }
        
        var entries = await GetEntries();
        var layer = Util.LayerForEntries(entries);
        if (layer == null)
        {
            foreach (var entry in entries)
            {
                if (entry is MST mst)
                {
                    var childLayer = await mst.AttemptGetLayer();
                    if (childLayer != null)
                    {
                        layer = childLayer + 1;
                        break;
                    }
                }
            }
        }
        
        if (layer != null)
        {
            Layer = layer;
        }
        
        return layer;
    }
    
    public async Task<(Cid cid, byte[] bytes)> Serialize()
    {
        var entries = await GetEntries();
        var outdated = entries.OfType<MST>().Where(x => x.OutdatedPointer).ToArray();
        if (outdated.Length > 0)
        {
            foreach (var mst in outdated)
            {
                await mst.GetPointer();
            }
            entries = await GetEntries();
        }
        
        var data = Util.SerializeNodeData(entries);
        var block = CborBlock.Encode(data);
        return (block.Cid, block.Bytes);
    }

    public static MST Load(IRepoStorage storage, Cid pointer, MstOpts? opts = null)
    {
        return new MST(storage, pointer, null, opts?.Layer);
    }
    
    public async Task<(Cid root, BlockMap blocks)> GetUnstoredBlocks()
    {
        var blocks = new BlockMap();
        var pointer = await GetPointer();
        var alreadyHas = await Storage.Has(pointer);
        if (alreadyHas)
        {
            return (pointer, blocks);
        }
        var entries = await GetEntries();
        var data = Util.SerializeNodeData(entries);
        blocks.Add(data.ToCborObject());
        foreach (var entry in entries)
        {
            if (entry is MST mst)
            {
                var (root, childBlocks) = await mst.GetUnstoredBlocks();
                blocks.AddMap(childBlocks);
            }
        }
        return (pointer, blocks);
    }
    
    public async Task<MST> Add(string key, Cid value, int? knownZeros = null)
    {
        Util.EnsureValidMstKey(key);
        var keyZeroes = knownZeros ??= Util.LeadingZerosOnHash(Encoding.UTF8.GetBytes(key));
        var layer = await GetLayer();
        var newLeaf = new Leaf(key, value);
        if (keyZeroes == layer)
        {
            // it belongs in this layer
            var index = await FindGtOrEqualLeafIndex(key);
            var first = await AtIndex(index);
            if (first is Leaf leaf && leaf.Key == key)
            {
                throw new Exception($"There is already a value at key: {key}");
            }
            var prevNode = await AtIndex(index - 1);
            if (prevNode is null or Leaf)
            {
                // if entry before is a leaf, (or we're on far left) we can just splice in
                return await SpliceIn(newLeaf, index);
            }
            else if (prevNode is MST mst)
            {
                // else we try to split the subtree around the key
                var splitSubTree = await mst.SplitAround(key);
                return await ReplaceWithSplit(index - 1, splitSubTree.left, newLeaf, splitSubTree.right);
            }
            else
            {
                throw new Exception("Invalid node type");
            }
        } 
        else if (keyZeroes < layer)
        {
            // it belongs on a lower layer
            var index = await FindGtOrEqualLeafIndex(key);
            var prevNode = await AtIndex(index - 1);
            if (prevNode is MST mst)
            {
                // if entry before is a tree, we add it to that tree
                var newSubtree = await mst.Add(key, value, keyZeroes);
                return await UpdateEntry(index - 1, newSubtree);
            }
            else
            {
                var subTree = await CreateChild();
                var newSubTree = await subTree.Add(key, value, keyZeroes);
                return await SpliceIn(newSubTree, index);
            }
        }
        else
        {
            // it belongs on a higher layer & we must push the rest of the tree down
            var (left, right) = await SplitAround(key);
            // if the newly added key has >=2 more leading zeros than the current highest layer
            // then we need to add in structural nodes in between as well
            var extraLayersToAdd = keyZeroes - layer;
            // intentionally starting at 1, since first layer is taken care of by split
            for (int i = 1; i < extraLayersToAdd; i++)
            {
                if (left != null)
                {
                    left = await left.CreateParent();
                }
                if (right != null)
                {
                    right = await right.CreateParent();
                }
            }

            var updated = new List<INodeEntry>();
            if (left != null)
            {
                updated.Add(left);
            }
            updated.Add(newLeaf);
            if (right != null)
            {
                updated.Add(right);
            }
            var newRoot = MST.Create(Storage, updated.ToArray(), new MstOpts(keyZeroes));
            newRoot.OutdatedPointer = true;
            return newRoot;
        }
    }

    public async Task<MST> Update(string key, Cid value)
    {
        Util.EnsureValidMstKey(key);
        var index = await FindGtOrEqualLeafIndex(key);
        var entry = await AtIndex(index);
        if (entry is Leaf leaf && leaf.Key == key)
        {
            return await UpdateEntry(index, new Leaf(key, value));
        }
        var prev = await AtIndex(index - 1);
        if (prev is MST mst)
        {
            var updatedTree = await mst.Update(key, value);
            return await UpdateEntry(index - 1, updatedTree);
        }
        
        throw new Exception($"Key not found: {key}");
    }

    public async Task<MST> Delete(string key)
    {
        var altered = await DeleteRecurse(key);
        return await altered.TrimTop();
    }
    
    public async Task<MST> TrimTop()
    {
        var entries = await GetEntries();
        if (entries is [MST mst])
        {
            return await mst.TrimTop();
        }
        
        return this;
    }

    public async Task<MST> DeleteRecurse(string key)
    {
        var index = await FindGtOrEqualLeafIndex(key);
        var found = await AtIndex(index);
        
        if (found is Leaf leaf && leaf.Key == key)
        {
            var prev = await AtIndex(index - 1);
            var next = await AtIndex(index + 1);
            if (prev is MST prevMst && next is MST nextMst)
            {
                var merged = await prevMst.AppendMerge(nextMst);
                return NewTree([
                    ..(await Slice(0, index - 1)),
                    merged,
                    ..(await Slice(index + 2, null))
                ]);
            }
            else
            {
                return await RemoveEntry(index);
            }
        }
        
        var prevNode = await AtIndex(index - 1);
        if (prevNode is MST mst)
        {
            var subtree = await mst.DeleteRecurse(key);
            var subtreeEntries = await subtree.GetEntries();
            if (subtreeEntries.Length == 0)
            {
                return await RemoveEntry(index - 1);
            }
            else
            {
                return await UpdateEntry(index - 1, subtree);
            }
        }
        else
        {
            throw new Exception($"Key not found: {key}");
        }
    }

    public async Task<MST> AppendMerge(MST toMerge)
    {
        if (await GetLayer() != await toMerge.GetLayer())
        {
            throw new Exception("Cannot merge trees of different layers");
        }
        
        var entries = await GetEntries();
        var toMergeEntries = await toMerge.GetEntries();
        var last = entries[^1];
        var first = toMergeEntries[0];
        if (last is MST lastMst && first is MST firstMst)
        {
            var merged = await lastMst.AppendMerge(firstMst);
            return NewTree([
                ..entries[..^1],
                merged,
                ..toMergeEntries[1..]
            ]);
        }
        else
        {
            return NewTree([..entries, ..toMergeEntries]);
        }
    }

    public async IAsyncEnumerable<INodeEntry> Walk()
    {
        yield return this;
        var entries = await GetEntries();
        foreach (var entry in entries)
        {
            if (entry is MST mst)
            {
                await foreach (var subEntry in mst.Walk())
                {
                    yield return subEntry;
                }
            }
            else
            {
                yield return entry;
            }
        }
    }

    public async Task<MST> CreateChild()
    {
        var layer = await GetLayer();
        return MST.Create(Storage, [], new MstOpts(layer - 1));
    }
    
    public async Task<MST> CreateParent()
    {
        var layer = await GetLayer();
        var parent = MST.Create(Storage, [this], new MstOpts(layer + 1));
        parent.OutdatedPointer = true;
        return parent;
    }
    
    public async Task<MST> ReplaceWithSplit(int index, MST? left, Leaf leaf, MST? right)
    {
        var update = (await Slice(0, index)).ToList();
        if (left != null) update.Add(left);
        update.Add(leaf);
        if (right != null) update.Add(right);
        update.AddRange(await Slice(index + 1, null));
        return NewTree(update.ToArray());
    }
    
    public async Task<MST> UpdateEntry(int index, INodeEntry entry)
    {
        var before = await Slice(0, index);
        var after = await Slice(index + 1, null);
        return NewTree([..before, entry, ..after]);
    }

    public async Task<MST> RemoveEntry(int index)
    {
        var before = await Slice(0, index);
        var after = await Slice(index + 1, null);
        return NewTree([..before, ..after]);
    }
    
    public async Task<MST> Prepend(INodeEntry entry)
    {
        var entries = await GetEntries();
        return NewTree([entry, ..entries]);
    }
    
    public async Task<MST> Append(INodeEntry entry)
    {
        var entries = await GetEntries();
        return NewTree([..entries, entry]);
    }
    
    public async Task<(MST? left, MST? right)> SplitAround(string key)
    {
        var index = await FindGtOrEqualLeafIndex(key);
        var leftData = await Slice(0, index);
        var rightData = await Slice(index, null);
        var left = NewTree(leftData);
        var right = NewTree(rightData);
        
        var lastInLeft = leftData[^1];
        if (lastInLeft is not Leaf)
        {
            left = await left.RemoveEntry(leftData.Length - 1);
            var split = await (lastInLeft as MST)!.SplitAround(key);
            if (split.left != null)
            {
                left = await left.Append(split.left);
            }
            if (split.right != null)
            {
                right = await right.Prepend(split.right);
            }
        }
        
        var outLeft = (await left.GetEntries()).Length > 0 ? left : null;
        var outRight = (await right.GetEntries()).Length > 0 ? right : null;
        return (outLeft, outRight);
    }

    public async Task<int> FindGtOrEqualLeafIndex(string key)
    {
        var entries = await GetEntries();
        var maybeIndex = entries.Select((entry, i) => (entry, i)).FirstOrDefault(x => x.entry is Leaf leaf && string.CompareOrdinal(leaf.Key, key) >= 0);
        return maybeIndex == default ? entries.Length : maybeIndex.i;
    }

    public async Task<INodeEntry?> AtIndex(int index)
    {
        var entries = await GetEntries();
        return index < entries.Length ? entries[index] : null;
    }
    
    public async Task<INodeEntry[]> Slice(int? start, int? end)
    {
        var entries = await GetEntries();
        start ??= 0;
        end ??= entries.Length;
        return entries[start.Value..end.Value];
    }

    public async Task<MST> SpliceIn(INodeEntry entry, int index)
    {
        var before = await Slice(0, index);
        var after = await Slice(index, null);
        return NewTree([..before, entry, ..after]);
    }
}

public record MstOpts(int Layer);

public class Leaf : INodeEntry
{
    public readonly string Key;
    public readonly Cid Value;
    
    public Leaf(string key, Cid value)
    {
        Key = key;
        Value = value;
    }
}

public interface INodeEntry;

public struct NodeData : ICborEncodable<NodeData>
{
    /// <summary>
    /// Left-most subtree
    /// </summary>
    public Cid? Left;
    
    /// <summary>
    /// Entries
    /// </summary>
    public List<TreeEntry> Entries;

    public CBORObject ToCborObject()
    {
        var obj = CBORObject.NewMap();
        if (Left != null)
        {
            obj.Add("left", Left.ToString());
        }
        
        var entries = CBORObject.NewArray();
        foreach (var entry in Entries)
        {
            entries.Add(entry.ToCborObject());
        }
        obj.Add("entries", entries);
        return obj;
    }

    public static NodeData FromCborObject(CBORObject obj)
    {
        var left = obj.ContainsKey("left") ? obj["left"].AsString() : null;
        var entries = obj["entries"].Values.Select(TreeEntry.FromCborObject).ToList();
        return new NodeData
        {
            Left = left != null ? Cid.FromString(left) : null,
            Entries = entries
        };
    }
}

public struct TreeEntry : ICborEncodable<TreeEntry>
{
    /// <summary>
    /// Prefix count of ascii chars that this key shares with the prev key
    /// </summary>
    public int PrefixCount;

    /// <summary>
    /// The rest of the key outside the shared prefix
    /// </summary>
    public byte[] Key;

    public Cid Value;
    
    /// <summary>
    /// Next subtree (to the right of the leaf)
    /// </summary>
    public Cid? Tree;
    
    public CBORObject ToCborObject()
    {
        var obj = CBORObject.NewMap();
        obj.Add("prefixCount", PrefixCount);
        obj.Add("key", Key);
        obj.Add("value", Value.ToString());
        if (Tree != null)
        {
            obj.Add("tree", Tree.ToString());
        }
        return obj;
    }
    
    public static TreeEntry FromCborObject(CBORObject obj)
    {
        var prefixCount = obj["prefixCount"].AsInt32();
        var key = obj["key"].GetByteString();
        var value = Cid.FromString(obj["value"].AsString());
        var tree = obj.ContainsKey("tree") ? obj["tree"].AsString() : null;
        return new TreeEntry
        {
            PrefixCount = prefixCount,
            Key = key,
            Value = value,
            Tree = tree != null ? Cid.FromString(tree) : null
        };
    }
}