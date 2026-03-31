using CID;
using PeterO.Cbor;

namespace Repo;

public interface IRepoStorage
{
    // Writable
    public Task<Cid?> GetRootAsync();
    public Task PutBlockAsync(Cid cid, byte[] block, string rev);
    public Task PutManyAsync(BlockMap blocks, string rev);
    public Task UpdateRootAsync(Cid cid, string rev);
    public Task ApplyCommitAsync(CommitData commit);

    // Readable
    public Task<byte[]?> GetBytesAsync(Cid cid);
    public Task<bool> HasAsync(Cid cid);
    public Task<(BlockMap blocks, Cid[] missing)> GetBlocksAsync(Cid[] cids);

    // Blockstore stuff
    public Task<(CBORObject obj, byte[] bytes)> ReadObjAndBytesAsync(Cid cid);
    public Task<(CBORObject obj, byte[] bytes)?> AttemptReadAsync(Cid cid);
}



public interface IBlobStore
{
    public Task<string> PutTempAsync(byte[] bytes);
    public Task<string> PutTempAsync(byte[] bytes, CancellationToken ct);
    public Task<string> PutTempAsync(Stream stream);
    public Task<string> PutTempAsync(Stream stream, CancellationToken ct);
    public Task<long> GetTempSizeAsync(string key);

    public Task PutPermanentAsync(Cid cid, byte[] bytes, CancellationToken ct);
    public Task PutPermanentAsync(Cid cid, byte[] bytes);

    public Task PutPermanentAsync(Cid cid, Stream stream, CancellationToken ct);
    public Task PutPermanentAsync(Cid cid, Stream stream);

    public Task MakePermanentAsync(string tmpKey, Cid cid);
    public Task<byte[]> GetBytesAsync(Cid cid);
    public Task<Stream> GetStreamAsync(Cid cid);

    public Task<Stream> GetTempStreamAsync(string key);

    public Task DeleteAsync(Cid cid);
    public Task DeleteManyAsync(Cid[] cid);
}