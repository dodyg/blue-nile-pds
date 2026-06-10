using CID;
using Crypto.Secp256k1;
using global::Repo;
using global::Repo.MST;
using PeterO.Cbor;

namespace ActorStore.Tests;

public class MSTTests
{
    [Test]
    public async Task WalkReachableAsync_ReturnsAllEntriesAsync()
    {
        var storage = new MemoryBlockStore(null);
        var mst = MST.Create(storage, []);
        var keypair = Secp256k1Keypair.Create(false);
        var repo = await global::Repo.Repo.CreateAsync(storage, "did:plc:test", keypair);

        var leaves = await repo.Data.LeavesAsync();
        var reachableLeaves = new List<Leaf>();
        await foreach (var leaf in repo.Data.WalkReachableAsync())
        {
            if (leaf is Leaf l)
            {
                reachableLeaves.Add(l);
            }
        }

        await Assert.That(reachableLeaves.Count).IsEqualTo(leaves.Count);
    }

    [Test]
    public async Task ReachableLeavesAsync_ReturnsOnlyLeavesAsync()
    {
        var storage = new MemoryBlockStore(null);
        var keypair = Secp256k1Keypair.Create(false);
        var repo = await global::Repo.Repo.CreateAsync(storage, "did:plc:test", keypair);

        var reachableLeaves = new List<Leaf>();
        await foreach (var leaf in repo.Data.ReachableLeavesAsync())
        {
            reachableLeaves.Add(leaf);
        }

        var allLeaves = await repo.Data.LeavesAsync();
        await Assert.That(reachableLeaves.Count).IsEqualTo(allLeaves.Count);
    }

    [Test]
    public async Task WalkReachableAsync_SkipsMissingBlocksAsync()
    {
        var storage = new MemoryBlockStore(null);
        var keypair = Secp256k1Keypair.Create(false);
        var repo = await global::Repo.Repo.CreateAsync(storage, "did:plc:test", keypair,
        [
            new RecordCreateOp("app.bsky.feed.post", "1", CBORObject.NewMap().Add("$type", "app.bsky.feed.post"))
        ]);

        // Remove a leaf block to simulate missing block
        var leaf = (await repo.Data.LeavesAsync()).First();
        var blocks = new BlockMap();
        blocks.Set(leaf.Value, []);
        storage = new MemoryBlockStore(blocks);
        var brokenMst = MST.Load(storage, repo.Data.Pointer);

        var reachable = new List<INodeEntry>();
        await foreach (var entry in brokenMst.WalkReachableAsync())
        {
            reachable.Add(entry);
        }

        await Assert.That(reachable.Count).IsGreaterThanOrEqualTo(1);
    }
}
