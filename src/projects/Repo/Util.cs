using Common;
using Crypto;
using Repo.Car;
using Cid = CID.Cid;

namespace Repo;

public static class Util
{
    public static Commit SignCommit(UnsignedCommit unsigned, IKeyPair keypair)
    {
        var encoded = CborBlock.Encode(unsigned);
        var sig = keypair.Sign(encoded.Bytes);
        return new Commit(unsigned.Did, unsigned.Data, unsigned.Rev, unsigned.Prev, sig);
    }

    public static async Task<byte[]> BlocksToCarFile(Cid? root, BlockMap blocks)
    {
        await using var writer = await CarMemoryWriter.Create(root);
        foreach (var entry in blocks.Entries)
        {
            await writer.Put(new CarBlock(entry.Cid, entry.Block));
        }

        return writer.Bytes.ToArray();
    }

    /// <summary>
    /// Yields CAR blocks for the given root and block map. <br/>
    /// Use it to stream CAR files without generating the entire file in memory.
    /// </summary>
    public static IEnumerable<byte[]> BlockMapToCarEnumerable(Cid root, BlockMap blocks)
    {
        yield return CarEncoder.EncodeRoots(root);

        foreach (var block in blocks.Entries)
        {
            yield return CarEncoder.EncodeBlock(new CarBlock(block.Cid, block.Block));
        }
    }


    /// <summary>
    /// Yields CAR blocks for the given root and async block enumerable. <br/>
    /// Use it to stream CAR files without generating the entire file in memory.
    /// </summary>
    public static async IAsyncEnumerable<byte[]> CarBlocksToCarAsyncEnumerable(Cid root, IAsyncEnumerable<CarBlock> blocks)
    {
        yield return CarEncoder.EncodeRoots(root);

        await foreach (var block in blocks)
        {
            yield return CarEncoder.EncodeBlock(block);
        }
    }
}