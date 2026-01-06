using System.Text;
using CID;
using Common;
using PeterO.Cbor;

namespace Repo.MST;

public record MST : INodeEntry
{
    public readonly IRepoStorage Storage;
    private INodeEntry[]? _entries;

    public MST(IRepoStorage storage, Cid pointer, INodeEntry[]? entries = null, int? layer = null)
    {
        Storage = storage;
        Pointer = pointer;
        _entries = entries;
        Layer = layer;
    }
    public Cid Pointer { get; private set; }
    public int? Layer { get; private set; }

    /// <summary>
    ///  We don't hash the node on every mutation for performance reasons
    ///  Instead we keep track of whether the pointer is outdated and only (recursively) calculate when needed
    /// </summary>
    public bool OutdatedPointer { get; private set; }

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
        _entries = Util.DeserializeNodeData(Storage, nodeData, layer == null ? null : new MstOpts(layer.Value));

        return _entries;
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
                throw new Exception($"There is already a value at key: {key} with value: {leaf.Value}");
            }
            var prevNode = await AtIndex(index - 1);
            if (prevNode is null or Leaf)
            {
                // if entry before is a leaf, (or we're on far left) we can just splice in
                return await SpliceIn(newLeaf, index);
            }
            if (prevNode is MST mst)
            {
                // else we try to split the subtree around the key
                var splitSubTree = await mst.SplitAround(key);
                return await ReplaceWithSplit(index - 1, splitSubTree.left, newLeaf, splitSubTree.right);
            }
            throw new Exception("Invalid node type");
        }
        if (keyZeroes < layer)
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
            var subTree = await CreateChild();
            var newSubTree = await subTree.Add(key, value, keyZeroes);
            return await SpliceIn(newSubTree, index);
        }
        // it belongs on a higher layer & we must push the rest of the tree down
        var (left, right) = await SplitAround(key);
        // if the newly added key has >=2 more leading zeros than the current highest layer
        // then we need to add in structural nodes in between as well
        var extraLayersToAdd = keyZeroes - layer;
        // intentionally starting at 1, since first layer is taken care of by split
        for (var i = 1; i < extraLayersToAdd; i++)
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
        var newRoot = Create(Storage, updated.ToArray(), new MstOpts(keyZeroes));
        newRoot.OutdatedPointer = true;
        return newRoot;
    }

    /// <summary>
    /// Gets the value at the given key
    /// </summary>
    public async Task<Cid?> Get(string key)
    {
        var index = await FindGtOrEqualLeafIndex(key);
        var found = await AtIndex(index);
        if (found is Leaf leaf && leaf.Key == key)
        {
            return leaf.Value;
        }
        var prev = await AtIndex(index - 1);
        if (prev is MST mst)
        {
            return await mst.Get(key);
        }
        return null;
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

        throw new Exception($"Could not find a record with key: {key}");
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
                    ..await Slice(0, index - 1),
                    merged,
                    ..await Slice(index + 2, null)
                ]);
            }
            return await RemoveEntry(index);
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
            return await UpdateEntry(index - 1, subtree);
        }
        throw new Exception($"Could not find a record with key: {key}");
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
        return NewTree([..entries, ..toMergeEntries]);
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

    /// <summary>
    /// Walk tree starting at key
    /// </summary>
    public async IAsyncEnumerable<INodeEntry> WalkFrom(string key)
    {
        yield return this;
        var index = await FindGtOrEqualLeafIndex(key);
        var entries = await GetEntries();
        var found = index < entries.Length ? entries[index] : null;
        if (found is Leaf foundLeaf && foundLeaf.Key == key)
        {
            yield return found;
        }
        else
        {
            var prev = index > 0 ? entries[index - 1] : null;
            if (prev != null)
            {
                if (prev is Leaf prevLeaf && prevLeaf.Key == key)
                {
                    yield return prev;
                }
                else if (prev is MST prevMst)
                {
                    await foreach (var subEntry in prevMst.WalkFrom(key))
                    {
                        yield return subEntry;
                    }
                }
            }
        }

        for (var i = index; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry is Leaf)
            {
                yield return entry;
            }
            else if (entry is MST mst)
            {
                await foreach (var subEntry in mst.WalkFrom(key))
                {
                    yield return subEntry;
                }
            }
        }
    }

    /// <summary>
    /// Walk leaves starting at key
    /// </summary>
    public async IAsyncEnumerable<Leaf> WalkLeavesFrom(string key)
    {
        await foreach (var node in WalkFrom(key))
        {
            if (node is Leaf leaf)
            {
                yield return leaf;
            }
        }
    }

    /// <summary>
    /// List leaves with optional count, after, and before filters
    /// </summary>
    public async Task<List<Leaf>> List(int count = int.MaxValue, string? after = null, string? before = null)
    {
        var vals = new List<Leaf>();
        await foreach (var leaf in WalkLeavesFrom(after ?? ""))
        {
            if (leaf.Key == after) continue;
            if (vals.Count >= count) break;
            if (before != null && string.CompareOrdinal(leaf.Key, before) >= 0) break;
            vals.Add(leaf);
        }
        return vals;
    }

    /// <summary>
    /// List leaves with a specific prefix
    /// </summary>
    public async Task<List<Leaf>> ListWithPrefix(string prefix, int count = int.MaxValue)
    {
        var vals = new List<Leaf>();
        await foreach (var leaf in WalkLeavesFrom(prefix))
        {
            if (vals.Count >= count || !leaf.Key.StartsWith(prefix)) break;
            vals.Add(leaf);
        }
        return vals;
    }

    /// <summary>
    /// Get all paths in the tree
    /// </summary>
    public async Task<List<List<INodeEntry>>> Paths()
    {
        var entries = await GetEntries();
        var paths = new List<List<INodeEntry>>();
        foreach (var entry in entries)
        {
            if (entry is Leaf)
            {
                paths.Add([entry]);
            }
            if (entry is MST mst)
            {
                var subPaths = await mst.Paths();
                foreach (var p in subPaths)
                {
                    var newPath = new List<INodeEntry> { mst };
                    newPath.AddRange(p);
                    paths.Add(newPath);
                }
            }
        }
        return paths;
    }

    /// <summary>
    /// Walks tree and returns all nodes
    /// </summary>
    public async Task<List<INodeEntry>> AllNodes()
    {
        var nodes = new List<INodeEntry>();
        await foreach (var entry in Walk())
        {
            nodes.Add(entry);
        }
        return nodes;
    }

    /// <summary>
    /// Walks tree and returns all leaves
    /// </summary>
    public async Task<List<Leaf>> Leaves()
    {
        var leaves = new List<Leaf>();
        await foreach (var entry in Walk())
        {
            if (entry is Leaf leaf)
            {
                leaves.Add(leaf);
            }
        }
        return leaves;
    }

    /// <summary>
    /// Returns total leaf count
    /// </summary>
    public async Task<int> LeafCount()
    {
        var leaves = await Leaves();
        return leaves.Count;
    }

    /// <summary>
    /// Walks tree and returns all CIDs
    /// </summary>
    public async Task<HashSet<Cid>> AllCids()
    {
        var cids = new HashSet<Cid>();
        var entries = await GetEntries();
        foreach (var entry in entries)
        {
            if (entry is Leaf leaf)
            {
                cids.Add(leaf.Value);
            }
            else if (entry is MST mst)
            {
                var subtreeCids = await mst.AllCids();
                foreach (var cid in subtreeCids)
                {
                    cids.Add(cid);
                }
            }
        }
        cids.Add(await GetPointer());
        return cids;
    }

    /// <summary>
    /// Get CIDs for path to a key
    /// </summary>
    public async Task<List<Cid>> CidsForPath(string key)
    {
        var cids = new List<Cid> { await GetPointer() };
        var index = await FindGtOrEqualLeafIndex(key);
        var found = await AtIndex(index);
        if (found is Leaf leaf && leaf.Key == key)
        {
            cids.Add(leaf.Value);
            return cids;
        }
        var prev = await AtIndex(index - 1);
        if (prev is MST mst)
        {
            cids.AddRange(await mst.CidsForPath(key));
        }
        return cids;
    }

    public async Task<MST> CreateChild()
    {
        var layer = await GetLayer();
        return Create(Storage, [], new MstOpts(layer - 1));
    }

    public async Task<MST> CreateParent()
    {
        var layer = await GetLayer();
        var parent = Create(Storage, [this], new MstOpts(layer + 1));
        parent.OutdatedPointer = true;
        return parent;
    }

    public async Task<MST> ReplaceWithSplit(int index, MST? left, Leaf leaf, MST? right)
    {
        var update = (await Slice(0, index)).ToList();
        if (left != null)
        {
            update.Add(left);
        }
        update.Add(leaf);
        if (right != null)
        {
            update.Add(right);
        }
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

        var lastInLeft = leftData.Length > 0 ? leftData[^1] : null;
        if (lastInLeft is MST mst)
        {
            left = await left.RemoveEntry(leftData.Length - 1);
            var split = await mst.SplitAround(key);
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
        var maybeIndex = entries.Select((entry, i) => (entry, i))
            .Where(x => x.entry is Leaf leaf && string.CompareOrdinal(leaf.Key, key) >= 0)
            .Select(x => x.i)
            .FirstOrDefault(-1);
        return maybeIndex >= 0 ? maybeIndex : entries.Length;
    }

    public async Task<INodeEntry?> AtIndex(int index)
    {
        var entries = await GetEntries();
        return index < entries.Length && index >= 0 ? entries[index] : null;
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
    ///     Left-most subtree
    /// </summary>
    public Cid? Left;

    /// <summary>
    ///     Entries
    /// </summary>
    public List<TreeEntry> Entries;

    public CBORObject ToCborObject()
    {
        var obj = CBORObject.NewMap();

        obj.Add("l", Left?.ToCBORObject());
        var entries = CBORObject.NewArray();
        foreach (var entry in Entries)
        {
            entries.Add(entry.ToCborObject());
        }
        obj.Add("e", entries);
        return obj;
    }

    public static NodeData FromCborObject(CBORObject obj)
    {
        Cid? left = obj.ContainsKey("l") && !obj["l"].IsNull ? Cid.FromCBOR(obj["l"]) : null;
        var entries = obj["e"].Values.Select(TreeEntry.FromCborObject).ToList();
        return new NodeData
        {
            Left = left,
            Entries = entries
        };
    }
}

public struct TreeEntry : ICborEncodable<TreeEntry>
{
    /// <summary>
    ///     Prefix count of ascii chars that this key shares with the prev key
    /// </summary>
    public int PrefixCount;

    /// <summary>
    ///     The rest of the key outside the shared prefix
    /// </summary>
    public byte[] Key;

    public string KeyString => Encoding.ASCII.GetString(Key);

    public Cid Value;

    /// <summary>
    ///     Next subtree (to the right of the leaf)
    /// </summary>
    public Cid? Tree;

    public CBORObject ToCborObject()
    {
        var obj = CBORObject.NewMap();
        obj.Add("p", PrefixCount);
        obj.Add("k", Key);
        obj.Add("v", Value.ToCBORObject());
        obj.Add("t", Tree?.ToCBORObject());
        return obj;
    }

    public static TreeEntry FromCborObject(CBORObject obj)
    {
        var prefixCount = obj["p"].AsInt32();
        var key = obj["k"].GetByteString();
        var value = Cid.FromCBOR(obj["v"]);
        Cid? tree = obj.ContainsKey("t") && !obj["t"].IsNull ? Cid.FromCBOR(obj["t"]) : null;
        return new TreeEntry
        {
            PrefixCount = prefixCount,
            Key = key,
            Value = value,
            Tree = tree
        };
    }
}