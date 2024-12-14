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
        await using var writer = await CarWriter.Create(root);
        foreach (var entry in blocks.Entries)
        {
            await writer.Put(new CarBlock(entry.Cid, entry.Block));
        }

        return writer.Bytes.ToArray();
    }
}