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

    public async Task<INodeEntry[]> GetEntriesAsync()
    {
        if (_entries != null)
        {
            return _entries;
        }

        var data = await Storage.ReadObjAndBytesAsync(Pointer);
        var nodeData = NodeData.FromCborObject(data.obj);
        TreeEntry? firstLeaf = nodeData.Entries.Count > 0 ? nodeData.Entries[0] : null;
        int? layer = firstLeaf != null ? Util.LeadingZerosOnHash(firstLeaf.Value.Key) : null;
        _entries = Util.DeserializeNodeData(Storage, nodeData, layer == null ? null : new MstOpts(layer.Value));

        return _entries;
    }

    public async Task<Cid> GetPointerAsync()
    {
        if (!OutdatedPointer)
        {
            return Pointer;
        }

        var (cid, bytes) = await SerializeAsync();
        Pointer = cid;
        OutdatedPointer = false;
        return Pointer;
    }

    public async Task<int> GetLayerAsync()
    {
        Layer = await AttemptGetLayerAsync() ?? 0;
        return Layer.Value;
    }

    public async Task<int?> AttemptGetLayerAsync()
    {
        if (Layer != null)
        {
            return Layer.Value;
        }

        var entries = await GetEntriesAsync();
        var layer = Util.LayerForEntries(entries);
        if (layer == null)
        {
            foreach (var entry in entries)
            {
                if (entry is MST mst)
                {
                    var childLayer = await mst.AttemptGetLayerAsync();
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

    public async Task<(Cid cid, byte[] bytes)> SerializeAsync()
    {
        var entries = await GetEntriesAsync();
        var outdated = entries.OfType<MST>().Where(x => x.OutdatedPointer).ToArray();
        if (outdated.Length > 0)
        {
            foreach (var mst in outdated)
            {
                await mst.GetPointerAsync();
            }
            entries = await GetEntriesAsync();
        }

        var data = Util.SerializeNodeData(entries);
        var block = CborBlock.Encode(data);
        return (block.Cid, block.Bytes);
    }

    public static MST Load(IRepoStorage storage, Cid pointer, MstOpts? opts = null)
    {
        return new MST(storage, pointer, null, opts?.Layer);
    }

    public async Task<(Cid root, BlockMap blocks)> GetUnstoredBlocksAsync()
    {
        var blocks = new BlockMap();
        var pointer = await GetPointerAsync();
        var alreadyHas = await Storage.HasAsync(pointer);
        if (alreadyHas)
        {
            return (pointer, blocks);
        }
        var entries = await GetEntriesAsync();
        var data = Util.SerializeNodeData(entries);
        blocks.Add(data.ToCborObject());
        foreach (var entry in entries)
        {
            if (entry is MST mst)
            {
                var (root, childBlocks) = await mst.GetUnstoredBlocksAsync();
                blocks.AddMap(childBlocks);
            }
        }
        return (pointer, blocks);
    }

    public async Task<MST> AddAsync(string key, Cid value, int? knownZeros = null)
    {
        Util.EnsureValidMstKey(key);
        var keyZeroes = knownZeros ??= Util.LeadingZerosOnHash(Encoding.UTF8.GetBytes(key));
        var layer = await GetLayerAsync();
        var newLeaf = new Leaf(key, value);
        if (keyZeroes == layer)
        {
            // it belongs in this layer
            var index = await FindGtOrEqualLeafIndexAsync(key);
            var first = await AtIndexAsync(index);
            if (first is Leaf leaf && leaf.Key == key)
            {
                throw new Exception($"There is already a value at key: {key} with value: {leaf.Value}");
            }
            var prevNode = await AtIndexAsync(index - 1);
            if (prevNode is null or Leaf)
            {
                // if entry before is a leaf, (or we're on far left) we can just splice in
                return await SpliceInAsync(newLeaf, index);
            }
            if (prevNode is MST mst)
            {
                // else we try to split the subtree around the key
                var splitSubTree = await mst.SplitAroundAsync(key);
                return await ReplaceWithSplitAsync(index - 1, splitSubTree.left, newLeaf, splitSubTree.right);
            }
            throw new Exception("Invalid node type");
        }
        if (keyZeroes < layer)
        {
            // it belongs on a lower layer
            var index = await FindGtOrEqualLeafIndexAsync(key);
            var prevNode = await AtIndexAsync(index - 1);
            if (prevNode is MST mst)
            {
                // if entry before is a tree, we add it to that tree
                var newSubtree = await mst.AddAsync(key, value, keyZeroes);
                return await UpdateEntryAsync(index - 1, newSubtree);
            }
            var subTree = await CreateChildAsync();
            var newSubTree = await subTree.AddAsync(key, value, keyZeroes);
            return await SpliceInAsync(newSubTree, index);
        }
        // it belongs on a higher layer & we must push the rest of the tree down
        var (left, right) = await SplitAroundAsync(key);
        // if the newly added key has >=2 more leading zeros than the current highest layer
        // then we need to add in structural nodes in between as well
        var extraLayersToAdd = keyZeroes - layer;
        // intentionally starting at 1, since first layer is taken care of by split
        for (var i = 1; i < extraLayersToAdd; i++)
        {
            if (left != null)
            {
                left = await left.CreateParentAsync();
            }
            if (right != null)
            {
                right = await right.CreateParentAsync();
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
    public async Task<Cid?> GetAsync(string key)
    {
        var index = await FindGtOrEqualLeafIndexAsync(key);
        var found = await AtIndexAsync(index);
        if (found is Leaf leaf && leaf.Key == key)
        {
            return leaf.Value;
        }
        var prev = await AtIndexAsync(index - 1);
        if (prev is MST mst)
        {
            return await mst.GetAsync(key);
        }
        return null;
    }

    public async Task<MST> UpdateAsync(string key, Cid value)
    {
        Util.EnsureValidMstKey(key);
        var index = await FindGtOrEqualLeafIndexAsync(key);
        var entry = await AtIndexAsync(index);
        if (entry is Leaf leaf && leaf.Key == key)
        {
            return await UpdateEntryAsync(index, new Leaf(key, value));
        }
        var prev = await AtIndexAsync(index - 1);
        if (prev is MST mst)
        {
            var updatedTree = await mst.UpdateAsync(key, value);
            return await UpdateEntryAsync(index - 1, updatedTree);
        }

        throw new Exception($"Could not find a record with key: {key}");
    }

    public async Task<MST> DeleteAsync(string key)
    {
        var altered = await DeleteRecurseAsync(key);
        return await altered.TrimTopAsync();
    }

    public async Task<MST> TrimTopAsync()
    {
        var entries = await GetEntriesAsync();
        if (entries is [MST mst])
        {
            return await mst.TrimTopAsync();
        }

        return this;
    }

    public async Task<MST> DeleteRecurseAsync(string key)
    {
        var index = await FindGtOrEqualLeafIndexAsync(key);
        var found = await AtIndexAsync(index);

        if (found is Leaf leaf && leaf.Key == key)
        {
            var prev = await AtIndexAsync(index - 1);
            var next = await AtIndexAsync(index + 1);
            if (prev is MST prevMst && next is MST nextMst)
            {
                var merged = await prevMst.AppendMergeAsync(nextMst);
                return NewTree([
                    ..await SliceAsync(0, index - 1),
                    merged,
                    ..await SliceAsync(index + 2, null)
                ]);
            }
            return await RemoveEntryAsync(index);
        }

        var prevNode = await AtIndexAsync(index - 1);
        if (prevNode is MST mst)
        {
            var subtree = await mst.DeleteRecurseAsync(key);
            var subtreeEntries = await subtree.GetEntriesAsync();
            if (subtreeEntries.Length == 0)
            {
                return await RemoveEntryAsync(index - 1);
            }
            return await UpdateEntryAsync(index - 1, subtree);
        }
        throw new Exception($"Could not find a record with key: {key}");
    }

    public async Task<MST> AppendMergeAsync(MST toMerge)
    {
        if (await GetLayerAsync() != await toMerge.GetLayerAsync())
        {
            throw new Exception("Cannot merge trees of different layers");
        }

        var entries = await GetEntriesAsync();
        var toMergeEntries = await toMerge.GetEntriesAsync();
        var last = entries[^1];
        var first = toMergeEntries[0];
        if (last is MST lastMst && first is MST firstMst)
        {
            var merged = await lastMst.AppendMergeAsync(firstMst);
            return NewTree([
                ..entries[..^1],
                merged,
                ..toMergeEntries[1..]
            ]);
        }
        return NewTree([..entries, ..toMergeEntries]);
    }

    public async IAsyncEnumerable<INodeEntry> WalkAsync()
    {
        yield return this;
        var entries = await GetEntriesAsync();
        foreach (var entry in entries)
        {
            if (entry is MST mst)
            {
                await foreach (var subEntry in mst.WalkAsync())
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
    public async IAsyncEnumerable<INodeEntry> WalkFromAsync(string key)
    {
        yield return this;
        var index = await FindGtOrEqualLeafIndexAsync(key);
        var entries = await GetEntriesAsync();
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
                    await foreach (var subEntry in prevMst.WalkFromAsync(key))
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
                await foreach (var subEntry in mst.WalkFromAsync(key))
                {
                    yield return subEntry;
                }
            }
        }
    }

    /// <summary>
    /// Walk leaves starting at key
    /// </summary>
    public async IAsyncEnumerable<Leaf> WalkLeavesFromAsync(string key)
    {
        await foreach (var node in WalkFromAsync(key))
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
    public async Task<List<Leaf>> ListAsync(int count = int.MaxValue, string? after = null, string? before = null)
    {
        var vals = new List<Leaf>();
        await foreach (var leaf in WalkLeavesFromAsync(after ?? ""))
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
    public async Task<List<Leaf>> ListWithPrefixAsync(string prefix, int count = int.MaxValue)
    {
        var vals = new List<Leaf>();
        await foreach (var leaf in WalkLeavesFromAsync(prefix))
        {
            if (vals.Count >= count || !leaf.Key.StartsWith(prefix)) break;
            vals.Add(leaf);
        }
        return vals;
    }

    /// <summary>
    /// Get all paths in the tree
    /// </summary>
    public async Task<List<List<INodeEntry>>> PathsAsync()
    {
        var entries = await GetEntriesAsync();
        var paths = new List<List<INodeEntry>>();
        foreach (var entry in entries)
        {
            if (entry is Leaf)
            {
                paths.Add([entry]);
            }
            if (entry is MST mst)
            {
                var subPaths = await mst.PathsAsync();
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
    public async Task<List<INodeEntry>> AllNodesAsync()
    {
        var nodes = new List<INodeEntry>();
        await foreach (var entry in WalkAsync())
        {
            nodes.Add(entry);
        }
        return nodes;
    }

    /// <summary>
    /// Walks tree and returns all leaves
    /// </summary>
    public async Task<List<Leaf>> LeavesAsync()
    {
        var leaves = new List<Leaf>();
        await foreach (var entry in WalkAsync())
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
    public async Task<int> LeafCountAsync()
    {
        var leaves = await LeavesAsync();
        return leaves.Count;
    }

    /// <summary>
    /// Walks tree and returns all CIDs
    /// </summary>
    public async Task<HashSet<Cid>> AllCidsAsync()
    {
        var cids = new HashSet<Cid>();
        var entries = await GetEntriesAsync();
        foreach (var entry in entries)
        {
            if (entry is Leaf leaf)
            {
                cids.Add(leaf.Value);
            }
            else if (entry is MST mst)
            {
                var subtreeCids = await mst.AllCidsAsync();
                foreach (var cid in subtreeCids)
                {
                    cids.Add(cid);
                }
            }
        }
        cids.Add(await GetPointerAsync());
        return cids;
    }

    /// <summary>
    /// Get CIDs for path to a key
    /// </summary>
    public async Task<List<Cid>> CidsForPathAsync(string key)
    {
        var cids = new List<Cid> { await GetPointerAsync() };
        var index = await FindGtOrEqualLeafIndexAsync(key);
        var found = await AtIndexAsync(index);
        if (found is Leaf leaf && leaf.Key == key)
        {
            cids.Add(leaf.Value);
            return cids;
        }
        var prev = await AtIndexAsync(index - 1);
        if (prev is MST mst)
        {
            cids.AddRange(await mst.CidsForPathAsync(key));
        }
        return cids;
    }

    public async Task<MST> CreateChildAsync()
    {
        var layer = await GetLayerAsync();
        return Create(Storage, [], new MstOpts(layer - 1));
    }

    public async Task<MST> CreateParentAsync()
    {
        var layer = await GetLayerAsync();
        var parent = Create(Storage, [this], new MstOpts(layer + 1));
        parent.OutdatedPointer = true;
        return parent;
    }

    public async Task<MST> ReplaceWithSplitAsync(int index, MST? left, Leaf leaf, MST? right)
    {
        var update = (await SliceAsync(0, index)).ToList();
        if (left != null)
        {
            update.Add(left);
        }
        update.Add(leaf);
        if (right != null)
        {
            update.Add(right);
        }
        update.AddRange(await SliceAsync(index + 1, null));
        return NewTree(update.ToArray());
    }

    public async Task<MST> UpdateEntryAsync(int index, INodeEntry entry)
    {
        var before = await SliceAsync(0, index);
        var after = await SliceAsync(index + 1, null);
        return NewTree([..before, entry, ..after]);
    }

    public async Task<MST> RemoveEntryAsync(int index)
    {
        var before = await SliceAsync(0, index);
        var after = await SliceAsync(index + 1, null);
        return NewTree([..before, ..after]);
    }

    public async Task<MST> PrependAsync(INodeEntry entry)
    {
        var entries = await GetEntriesAsync();
        return NewTree([entry, ..entries]);
    }

    public async Task<MST> AppendAsync(INodeEntry entry)
    {
        var entries = await GetEntriesAsync();
        return NewTree([..entries, entry]);
    }

    public async Task<(MST? left, MST? right)> SplitAroundAsync(string key)
    {
        var index = await FindGtOrEqualLeafIndexAsync(key);
        var leftData = await SliceAsync(0, index);
        var rightData = await SliceAsync(index, null);
        var left = NewTree(leftData);
        var right = NewTree(rightData);

        var lastInLeft = leftData.Length > 0 ? leftData[^1] : null;
        if (lastInLeft is MST mst)
        {
            left = await left.RemoveEntryAsync(leftData.Length - 1);
            var split = await mst.SplitAroundAsync(key);
            if (split.left != null)
            {
                left = await left.AppendAsync(split.left);
            }
            if (split.right != null)
            {
                right = await right.PrependAsync(split.right);
            }
        }

        var outLeft = (await left.GetEntriesAsync()).Length > 0 ? left : null;
        var outRight = (await right.GetEntriesAsync()).Length > 0 ? right : null;
        return (outLeft, outRight);
    }

    public async Task<int> FindGtOrEqualLeafIndexAsync(string key)
    {
        var entries = await GetEntriesAsync();
        var maybeIndex = entries.Select((entry, i) => (entry, i))
            .Where(x => x.entry is Leaf leaf && string.CompareOrdinal(leaf.Key, key) >= 0)
            .Select(x => x.i)
            .FirstOrDefault(-1);
        return maybeIndex >= 0 ? maybeIndex : entries.Length;
    }

    public async Task<INodeEntry?> AtIndexAsync(int index)
    {
        var entries = await GetEntriesAsync();
        return index < entries.Length && index >= 0 ? entries[index] : null;
    }

    public async Task<INodeEntry[]> SliceAsync(int? start, int? end)
    {
        var entries = await GetEntriesAsync();
        start ??= 0;
        end ??= entries.Length;
        return entries[start.Value..end.Value];
    }

    public async Task<MST> SpliceInAsync(INodeEntry entry, int index)
    {
        var before = await SliceAsync(0, index);
        var after = await SliceAsync(index, null);
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