using System.Text.Json.Serialization;
using CID;

namespace Repo.MST;

public static class MSTDiff
{
    public static async Task<DataDiff> NullDiff(MST tree)
    {
        var diff = new DataDiff();
        await foreach (var entry in tree.Walk())
        {
            await diff.NodeAdd(entry);
        }
        return diff;
    }

    public static async Task<DataDiff> Diff(MST curr, MST? prev)
    {
        await curr.GetPointer();
        if (prev == null)
        {
            return await NullDiff(curr);
        }

        await prev.GetPointer();
        var diff = new DataDiff();

        var leftWalker = new MSTWalker(prev);
        var rightWalker = new MSTWalker(curr);
        while (!leftWalker.Status.Done || !rightWalker.Status.Done)
        {
            // if one walker is finished, continue walking the other & logging all nodes
            if (leftWalker.Status.Done && rightWalker.Status is WalkerStatusProgress rightProgress)
            {
                await diff.NodeAdd(rightProgress.Current);
                await rightWalker.Advance();
                continue;
            }
            if (leftWalker.Status is WalkerStatusProgress leftProgress && rightWalker.Status.Done)
            {
                await diff.NodeDelete(leftProgress.Current);
                await leftWalker.Advance();
                continue;
            }

            if (leftWalker.Status.Done || rightWalker.Status.Done)
            {
                break;
            }

            var left = (leftWalker.Status as WalkerStatusProgress)?.Current;
            var right = (rightWalker.Status as WalkerStatusProgress)?.Current;
            if (left == null || right == null)
            {
                break;
            }

            // if both pointers are leaves, record an update & advance both or record the lowest key and advance that pointer
            if (left is Leaf leftLeaf && right is Leaf rightLeaf)
            {
                if (leftLeaf.Key == rightLeaf.Key)
                {
                    if (!leftLeaf.Value.Equals(rightLeaf.Value))
                    {
                        diff.LeafUpdate(leftLeaf.Key, leftLeaf.Value, rightLeaf.Value);
                    }
                    await leftWalker.Advance();
                    await rightWalker.Advance();
                }
                else if (string.CompareOrdinal(leftLeaf.Key, rightLeaf.Key) < 0)
                {
                    await diff.NodeDelete(leftLeaf);
                    await leftWalker.Advance();
                }
                else
                {
                    await diff.NodeAdd(rightLeaf);
                    await rightWalker.Advance();
                }

                continue;
            }

            // next, ensure that we're on the same layer
            // if one walker is at a higher layer than the other, we need to do one of two things
            // if the higher walker is pointed at a tree, step into that tree to try to catch up with the lower
            // if the higher walker is pointed at a leaf, then advance the lower walker to try to catch up the higher
            if (leftWalker.Layer() > rightWalker.Layer())
            {
                if (left is Leaf)
                {
                    await diff.NodeAdd(right);
                    await rightWalker.Advance();
                }
                else
                {
                    await diff.NodeDelete(left);
                    await leftWalker.StepInto();
                }
                continue;
            }

            if (leftWalker.Layer() < rightWalker.Layer())
            {
                if (right is Leaf)
                {
                    await diff.NodeDelete(left);
                    await leftWalker.Advance();
                }
                else
                {
                    await diff.NodeAdd(right);
                    await rightWalker.StepInto();
                }
                continue;
            }

            // if we're on the same level, and both pointers are trees, do a comparison
            // if they're the same, step over. if they're different, step in to find the subdiff
            if (left is MST leftMst && right is MST rightMst)
            {
                if (leftMst.Pointer == rightMst.Pointer)
                {
                    await leftWalker.StepOver();
                    await rightWalker.StepOver();
                }
                else
                {
                    await diff.NodeAdd(right);
                    await diff.NodeDelete(left);
                    await leftWalker.StepInto();
                    await rightWalker.StepInto();
                }
                continue;
            }

            if (left is Leaf && right is MST)
            {
                await diff.NodeAdd(right);
                await rightWalker.StepInto();
                continue;
            }
            if (left is MST && right is Leaf)
            {
                await diff.NodeDelete(left);
                await leftWalker.StepInto();
                continue;
            }

            throw new Exception("Unidentifiable case in diff walk");
        }

        return diff;
    }
}

public class DataDiff
{

    [JsonPropertyName("adds")] public Dictionary<string, DataAdd> Adds { get; set; } = new();

    [JsonPropertyName("updates")] public Dictionary<string, DataUpdate> Updates { get; set; } = new();

    [JsonPropertyName("deletes")] public Dictionary<string, DataDelete> Deletes { get; set; } = new();

    [JsonPropertyName("newMstBlocks")] public BlockMap NewMstBlocks { get; set; } = new();

    [JsonPropertyName("newLeafCids")] public CidSet NewLeafCids { get; set; } = new();

    [JsonPropertyName("removedCids")] public CidSet RemovedCids { get; set; } = new();

    public static async Task<DataDiff> Of(MST curr, MST? prev)
    {
        return await MSTDiff.Diff(curr, prev);
    }

    public async Task NodeAdd(INodeEntry entry)
    {
        if (entry is Leaf leaf)
        {
            LeafAdd(leaf.Key, leaf.Value);
        }
        else if (entry is MST tree)
        {
            var (cid, bytes) = await tree.Serialize();
            TreeAdd(cid, bytes);
        }
    }

    public async Task NodeDelete(INodeEntry entry)
    {
        if (entry is Leaf leaf)
        {
            Deletes[leaf.Key] = new DataDelete(leaf.Key, leaf.Value);
            RemovedCids.Add(leaf.Value);
        }
        else if (entry is MST tree)
        {
            var cid = await tree.GetPointer();
            TreeDelete(cid);
        }
        else
        {
            throw new Exception("Unknown node type");
        }
    }

    public void LeafAdd(string key, Cid cid)
    {
        Adds[key] = new DataAdd(key, cid);
        if (RemovedCids.Has(cid))
        {
            RemovedCids.Delete(cid);
        }
        else
        {
            NewLeafCids.Add(cid);
        }
    }

    public void LeafUpdate(string key, Cid prev, Cid cid)
    {
        if (prev.Equals(cid))
        {
            return;
        }

        Updates[key] = new DataUpdate(key, prev, cid);
        RemovedCids.Add(cid);
        NewLeafCids.Add(cid);
    }

    public void LeafDelete(string key, Cid cid)
    {
        Deletes[key] = new DataDelete(key, cid);
        if (NewLeafCids.Has(cid))
        {
            NewLeafCids.Delete(cid);
        }
        else
        {
            RemovedCids.Add(cid);
        }
    }

    public void TreeAdd(Cid cid, byte[] bytes)
    {
        if (RemovedCids.Has(cid))
        {
            RemovedCids.Delete(cid);
        }
        else
        {
            NewMstBlocks.Set(cid, bytes);
        }
    }

    public void TreeDelete(Cid cid)
    {
        if (NewMstBlocks.Has(cid))
        {
            NewMstBlocks.Delete(cid);
        }
        else
        {
            RemovedCids.Add(cid);
        }
    }
}

public record DataAdd([property: JsonPropertyName("key")] string Key, [property: JsonPropertyName("cid")] Cid Cid);
public record DataUpdate([property: JsonPropertyName("key")] string Key, [property: JsonPropertyName("prev")] Cid Prev, [property: JsonPropertyName("cid")] Cid Cid);
public record DataDelete([property: JsonPropertyName("key")] string Key, [property: JsonPropertyName("cid")] Cid Cid);

public class CidSet
{
    private readonly HashSet<string> _set;

    public CidSet(Cid[]? arr = null)
    {
        arr ??= [];
        _set = [..arr.Select(c => c.ToString())];
    }

    public CidSet Add(Cid cid)
    {
        _set.Add(cid.ToString());
        return this;
    }

    public CidSet AddSet(CidSet toMerge)
    {
        foreach (var cid in toMerge.ToArray())
        {
            Add(cid);
        }
        return this;
    }

    public CidSet SubtractSet(CidSet toSubtract)
    {
        foreach (var cid in toSubtract.ToArray())
        {
            Delete(cid);
        }
        return this;
    }

    public CidSet Delete(Cid cid)
    {
        _set.Remove(cid.ToString());
        return this;
    }

    public bool Has(Cid cid)
    {
        return _set.Contains(cid.ToString());
    }

    public int Size()
    {
        return _set.Count;
    }

    public CidSet Clear()
    {
        _set.Clear();
        return this;
    }

    public Cid[] ToArray()
    {
        return _set.Select(Cid.FromString).ToArray();
    }
}