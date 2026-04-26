using CID;
using Common;
using Repo.MST;

namespace Repo.Tests;

public class MstTests
{
    private static MemoryBlockStore CreateStorage()
    {
        return new MemoryBlockStore(null);
    }

    private static MST.MST CreateEmptyMst()
    {
        var storage = CreateStorage();
        return MST.MST.Create(storage, []);
    }

    [Test]
    public async Task EmptyMst_HasNoLeaves()
    {
        var mst = CreateEmptyMst();

        var leaves = await mst.LeavesAsync();

        await Assert.That(leaves.Count).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyMst_LeafCountIsZero()
    {
        var mst = CreateEmptyMst();

        var count = await mst.LeafCountAsync();

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyMst_GetReturnsNull()
    {
        var mst = CreateEmptyMst();

        var result = await mst.GetAsync("app.bsky.feed.post/abc123");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AddEntry_CanRetrieve()
    {
        var storage = CreateStorage();
        var mst = MST.MST.Create(storage, []);
        var cid = Cid.Create("test-value-1");
        var key = $"app.bsky.feed.post/{TID.NextStr()}";

        var updated = await mst.AddAsync(key, cid);
        var result = await updated.GetAsync(key);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ToString()).IsEqualTo(cid.ToString());
    }

    [Test]
    public async Task AddMultipleEntries_AllRetrievable()
    {
        var storage = CreateStorage();
        var mst = MST.MST.Create(storage, []);
        var cid1 = Cid.Create("value-1");
        var cid2 = Cid.Create("value-2");
        var cid3 = Cid.Create("value-3");
        var key1 = $"app.bsky.feed.post/{TID.NextStr()}";
        var key2 = $"app.bsky.feed.post/{TID.NextStr()}";
        var key3 = $"app.bsky.feed.like/{TID.NextStr()}";

        mst = await mst.AddAsync(key1, cid1);
        mst = await mst.AddAsync(key2, cid2);
        mst = await mst.AddAsync(key3, cid3);

        var result1 = await mst.GetAsync(key1);
        var result2 = await mst.GetAsync(key2);
        var result3 = await mst.GetAsync(key3);

        await Assert.That(result1!.ToString()).IsEqualTo(cid1.ToString());
        await Assert.That(result2!.ToString()).IsEqualTo(cid2.ToString());
        await Assert.That(result3!.ToString()).IsEqualTo(cid3.ToString());
    }

    [Test]
    public async Task AddMultipleEntries_LeafCountMatches()
    {
        var storage = CreateStorage();
        var mst = MST.MST.Create(storage, []);
        var cid1 = Cid.Create("value-a");
        var cid2 = Cid.Create("value-b");
        var key1 = $"app.bsky.feed.post/{TID.NextStr()}";
        var key2 = $"app.bsky.feed.post/{TID.NextStr()}";

        mst = await mst.AddAsync(key1, cid1);
        mst = await mst.AddAsync(key2, cid2);

        var count = await mst.LeafCountAsync();

        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteEntry_NoLongerRetrievable()
    {
        var storage = CreateStorage();
        var mst = MST.MST.Create(storage, []);
        var cid = Cid.Create("delete-me");
        var key = $"app.bsky.feed.post/{TID.NextStr()}";

        mst = await mst.AddAsync(key, cid);
        mst = await mst.DeleteAsync(key);

        var result = await mst.GetAsync(key);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task DeleteOneEntry_OtherEntriesRemain()
    {
        var storage = CreateStorage();
        var mst = MST.MST.Create(storage, []);
        var cid1 = Cid.Create("keep-me");
        var cid2 = Cid.Create("delete-me");
        var key1 = $"app.bsky.feed.post/{TID.NextStr()}";
        var key2 = $"app.bsky.feed.post/{TID.NextStr()}";

        mst = await mst.AddAsync(key1, cid1);
        mst = await mst.AddAsync(key2, cid2);
        mst = await mst.DeleteAsync(key2);

        var result1 = await mst.GetAsync(key1);
        var result2 = await mst.GetAsync(key2);

        await Assert.That(result1!.ToString()).IsEqualTo(cid1.ToString());
        await Assert.That(result2).IsNull();
    }

    [Test]
    public async Task DeleteAllEntries_TreeIsEmpty()
    {
        var storage = CreateStorage();
        var mst = MST.MST.Create(storage, []);
        var cid = Cid.Create("temp");
        var key = $"app.bsky.feed.post/{TID.NextStr()}";

        mst = await mst.AddAsync(key, cid);
        mst = await mst.DeleteAsync(key);

        var count = await mst.LeafCountAsync();

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task WalkAsync_EmptyTree_YieldsOnlyRoot()
    {
        var mst = CreateEmptyMst();
        var nodes = new List<INodeEntry>();

        await foreach (var node in mst.WalkAsync())
        {
            nodes.Add(node);
        }

        await Assert.That(nodes.Count).IsEqualTo(1);
        await Assert.That(nodes[0]).IsTypeOf<MST.MST>();
    }

    [Test]
    public async Task WalkAsync_WithEntries_YieldsAllNodes()
    {
        var storage = CreateStorage();
        var mst = MST.MST.Create(storage, []);
        var cid = Cid.Create("walk-value");
        var key = $"app.bsky.feed.post/{TID.NextStr()}";

        mst = await mst.AddAsync(key, cid);

        var nodes = new List<INodeEntry>();
        await foreach (var node in mst.WalkAsync())
        {
            nodes.Add(node);
        }

        var leaves = nodes.OfType<Leaf>().ToList();
        await Assert.That(leaves.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(leaves[0].Key).IsEqualTo(key);
    }
}
