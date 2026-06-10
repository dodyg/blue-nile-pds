using System.Text.Json;
using ActorStore.Repo;
using CID;
using Common;

namespace ActorStore.Tests;

public class CidComputationTests
{
    [Test]
    public async Task CidForSafeRecord_IsDeterministicRegardlessOfKeyOrderAsync()
    {
        var json1 = """{"$type":"app.bsky.feed.post","text":"hello","createdAt":"2024-01-01T00:00:00Z"}""";
        var json2 = """{"createdAt":"2024-01-01T00:00:00Z","text":"hello","$type":"app.bsky.feed.post"}""";

        var el1 = JsonDocument.Parse(json1).RootElement;
        var el2 = JsonDocument.Parse(json2).RootElement;

        var cid1 = Prepare.CidForSafeRecord(el1);
        var cid2 = Prepare.CidForSafeRecord(el2);

        await Assert.That(cid1).IsEqualTo(cid2);
    }

    [Test]
    public async Task CidForSafeRecord_MatchesCanonicalDagCborAsync()
    {
        var json = """{"$type":"app.bsky.feed.post","text":"hello","createdAt":"2024-01-01T00:00:00Z"}""";
        var element = JsonDocument.Parse(json).RootElement;

        var actualCid = Prepare.CidForSafeRecord(element);
        var canonicalBytes = CanonicalDagCbor.Encode(element);
        var expectedHash = Multiformats.Hash.Multihash.Encode(
            System.Security.Cryptography.SHA256.HashData(canonicalBytes),
            Multiformats.Hash.HashType.SHA2_256);
        var expectedCid = Cid.NewV1((ulong)Multiformats.Codec.MulticodecCode.MerkleDAGCBOR, expectedHash);

        await Assert.That(actualCid).IsEqualTo(expectedCid);
    }

    [Test]
    public async Task CidForSafeRecord_NestedObjectsAreSortedAsync()
    {
        var json = """{"z":"last","a":"first","m":"middle"}""";
        var element = JsonDocument.Parse(json).RootElement;

        var bytes = CanonicalDagCbor.Encode(element);
        // CBOR map with 3 entries, keys sorted: a, m, z
        // First byte should be 0xA3 (map of 3 items)
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA3);
        // Next is key "a" (0x61 0x61) then value "first" (0x65 ...)
        await Assert.That(bytes[1]).IsEqualTo((byte)0x61);
        await Assert.That(bytes[2]).IsEqualTo((byte)'a');
    }
}
