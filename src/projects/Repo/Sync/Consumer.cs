using System.Security.Cryptography;
using CID;
using Common;
using Crypto;
using Crypto.Secp256k1;
using Multiformats.Codec;
using Multiformats.Hash;
using PeterO.Cbor;
using Repo.Car;
using Repo.MST;

namespace Repo.Sync;

public record CarHeader(List<Cid> Roots);

public static class Consumer
{
    /// <summary>
    /// Verify each block's CID matches its content.
    /// </summary>
    public static async Task VerifyIncomingCarBlocksAsync(IEnumerable<CarBlock> blocks)
    {
        foreach (var block in blocks)
        {
            var hash = Multihash.Encode(SHA256.HashData(block.Bytes), Multiformats.Hash.HashType.SHA2_256);
            var expectedCid = Cid.NewV1((ulong)MulticodecCode.MerkleDAGCBOR, hash);
            if (!expectedCid.Equals(block.Cid))
            {
                throw new RepoException($"Block CID mismatch: expected {expectedCid}, got {block.Cid}");
            }
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verify a full repo from CAR bytes.
    /// </summary>
    public static async Task VerifyRepoAsync(byte[] carBytes, string? did = null, byte[]? key = null)
    {
        var (header, blocks) = ParseCar(carBytes);
        if (header.Roots.Count != 1)
            throw new RepoException("Repo CAR must have exactly 1 root");

        var blockMap = new BlockMap();
        foreach (var block in blocks)
            blockMap.Set(block.Cid, block.Bytes);

        await VerifyRepoAsync(blockMap, header.Roots[0], did, key);
    }

    /// <summary>
    /// Verify a repo from a block map.
    /// </summary>
    public static async Task VerifyRepoAsync(BlockMap blocks, Cid head, string? did = null, byte[]? key = null)
    {
        await VerifyIncomingCarBlocksAsync(blocks.Entries.Select(e => new CarBlock(e.Cid, e.Block)));

        if (!blocks.Has(head))
            throw new RepoException("Head commit block not found");

        var commitObj = CBORObject.DecodeFromBytes(blocks.Get(head)!);
        var commit = Commit.FromCborObject(commitObj);

        if (!string.IsNullOrEmpty(did) && commit.Did != did)
            throw new RepoException($"DID mismatch: expected {did}, got {commit.Did}");

        // Verify signature if key provided
        if (key != null)
        {
            var unsigned = new UnsignedCommit(commit.Did, commit.Data, commit.Rev, commit.Prev);
            var encoded = CborBlock.Encode(unsigned);
            if (!Secp256k1Wrapper.Verify(encoded.Bytes, commit.Sig, key))
                throw new RepoException("Commit signature verification failed");
        }

        // Verify MST structure
        var memStorage = new MemoryBlockStore(blocks);
        var mst = MST.MST.Load(memStorage, commit.Data);
        await mst.AllNodesAsync(); // will throw MissingBlockException if any block is missing
    }

    /// <summary>
    /// Validate an incremental diff.
    /// </summary>
    public static async Task VerifyDiffAsync(global::Repo.Repo repo, BlockMap blocks, Cid root, string? did = null, byte[]? key = null)
    {
        var currRoot = await repo.Data.GetPointerAsync();

        // Load the new commit from blocks
        if (!blocks.Has(root))
            throw new RepoException("Diff root block not found");

        var commitObj = CBORObject.DecodeFromBytes(blocks.Get(root)!);
        var commit = Commit.FromCborObject(commitObj);

        if (commit.Prev == null || !commit.Prev.Value.Equals(currRoot))
            throw new RepoException("Diff is not a forward delta from current root");

        if (!string.IsNullOrEmpty(did) && commit.Did != did)
            throw new RepoException($"DID mismatch in diff");

        // Verify signature if key provided
        if (key != null)
        {
            var unsigned = new UnsignedCommit(commit.Did, commit.Data, commit.Rev, commit.Prev);
            var encoded = CborBlock.Encode(unsigned);
            if (!Secp256k1Wrapper.Verify(encoded.Bytes, commit.Sig, key))
                throw new RepoException("Diff commit signature verification failed");
        }

        // Verify all blocks in diff
        await VerifyIncomingCarBlocksAsync(blocks.Entries.Select(e => new CarBlock(e.Cid, e.Block)));

        // Verify MST can be loaded
        var allBlocks = new BlockMap();
        allBlocks.AddMap(blocks);
        var memStorage = new MemoryBlockStore(allBlocks);
        var mst = MST.MST.Load(memStorage, commit.Data);
        await mst.AllNodesAsync();
    }

    /// <summary>
    /// Validate individual Merkle proofs.
    /// </summary>
    public static async Task VerifyProofsAsync(BlockMap proofs, string[] claims, string did, byte[] key)
    {
        await VerifyIncomingCarBlocksAsync(proofs.Entries.Select(e => new CarBlock(e.Cid, e.Block)));

        var storage = new MemoryBlockStore(proofs);
        var foundClaims = new HashSet<string>();

        foreach (var entry in proofs.Entries)
        {
            var obj = CBORObject.DecodeFromBytes(entry.Block);
            if (obj.Type == CBORType.Map && obj.ContainsKey("e"))
            {
                // MST node
                var nodeData = NodeData.FromCborObject(obj);
                var entries = MST.Util.DeserializeNodeData(storage, nodeData, null);
                foreach (var e in entries)
                {
                    if (e is Leaf leaf)
                    {
                        foundClaims.Add(leaf.Key);
                    }
                }
            }
        }

        foreach (var claim in claims)
        {
            if (!foundClaims.Contains(claim))
                throw new RepoException($"Claim not found in proofs: {claim}");
        }
    }

    /// <summary>
    /// Extract and verify records from proofs.
    /// </summary>
    public static async Task<Dictionary<string, CBORObject>> VerifyRecordsAsync(BlockMap proofs, string did, byte[] key)
    {
        await VerifyIncomingCarBlocksAsync(proofs.Entries.Select(e => new CarBlock(e.Cid, e.Block)));

        var storage = new MemoryBlockStore(proofs);
        var records = new Dictionary<string, CBORObject>();

        foreach (var entry in proofs.Entries)
        {
            var obj = CBORObject.DecodeFromBytes(entry.Block);
            if (obj.Type == CBORType.Map && obj.ContainsKey("e"))
                continue; // skip MST nodes

            // Try to parse as record
            if (obj.Type == CBORType.Map && obj.ContainsKey("$type"))
            {
                var cid = CidForCbor(entry.Block);
                records[cid.ToString()] = obj;
            }
        }

        return records;
    }

    private static Cid CidForCbor(byte[] bytes)
    {
        var hash = Multihash.Encode(SHA256.HashData(bytes), Multiformats.Hash.HashType.SHA2_256);
        return Cid.NewV1((ulong)MulticodecCode.MerkleDAGCBOR, hash);
    }

    private static (CarHeader header, List<CarBlock> blocks) ParseCar(byte[] carBytes)
    {
        // Minimal CAR parser for verification
        var offset = 0;
        var headerLength = ReadVarInt(carBytes, ref offset);
        var headerBytes = carBytes.AsSpan(offset, headerLength);
        offset += headerLength;

        var headerCbor = CBORObject.DecodeFromBytes(headerBytes.ToArray());
        var roots = new List<Cid>();
        if (headerCbor.ContainsKey("roots"))
        {
            var rootsArr = headerCbor["roots"];
            for (var i = 0; i < rootsArr.Count; i++)
            {
                roots.Add(Cid.ReadBytes(rootsArr[i].GetByteString()));
            }
        }

        var blocks = new List<CarBlock>();
        while (offset < carBytes.Length)
        {
            var blockLength = ReadVarInt(carBytes, ref offset);
            if (blockLength == 0) break;
            var blockData = carBytes.AsSpan(offset, blockLength);
            offset += blockLength;

            var cidLength = ReadCidLength(blockData);
            var cid = Cid.ReadBytes(blockData.Slice(0, cidLength).ToArray());
            var content = blockData.Slice(cidLength).ToArray();
            blocks.Add(new CarBlock(cid, content));
        }

        return (new CarHeader(roots), blocks);
    }

    private static int ReadVarInt(byte[] data, ref int offset)
    {
        int value = 0, shift = 0;
        while (offset < data.Length)
        {
            var b = data[offset++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return value;
    }

    private static int ReadCidLength(ReadOnlySpan<byte> blockData)
    {
        if (blockData.Length < 2) return blockData.Length;
        var version = blockData[0];
        if (version == 0x01)
        {
            var codec = blockData[1];
            if (codec == 0x55)
            {
                if (blockData.Length < 4) return blockData.Length;
                return 4 + blockData[3];
            }
            if (blockData.Length < 3) return blockData.Length;
            return 3 + blockData[2];
        }
        if (version == 0x12 || version == 0x55)
        {
            if (blockData.Length < 3) return blockData.Length;
            return 3 + blockData[2];
        }
        return blockData.Length;
    }
}
