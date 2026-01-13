using CID;
using PeterO.Cbor;

namespace Repo;

public interface IRepoStorage
{
    // Writable
    public Task<Cid?> GetRoot();
    public Task PutBlock(Cid cid, byte[] block, string rev);
    public Task PutMany(BlockMap blocks, string rev);
    public Task UpdateRoot(Cid cid, string rev);
    public Task ApplyCommit(CommitData commit);

    // Readable
    public Task<byte[]?> GetBytes(Cid cid);
    public Task<bool> Has(Cid cid);
    public Task<(BlockMap blocks, Cid[] missing)> GetBlocks(Cid[] cids);

    // Blockstore stuff
    public Task<(CBORObject obj, byte[] bytes)> ReadObjAndBytes(Cid cid);
    public Task<(CBORObject obj, byte[] bytes)?> AttemptRead(Cid cid);
}



public interface IBlobStore
{
    public Task PutTemp(Cid cid, byte[] bytes);
    public Task PutTemp(Cid cid, Stream stream);

    public Task PutPermanent(Cid cid, byte[] bytes);
    public Task PutPermanent(Cid cid, Stream stream);

    public Task MakePermanent(Cid cid);
    public Task<byte[]> GetBytes(Cid cid);
    public Task<Stream> GetStream(Cid cid);

    public Task Delete(Cid cid);
    public Task DeleteMany(Cid[] cid);
}