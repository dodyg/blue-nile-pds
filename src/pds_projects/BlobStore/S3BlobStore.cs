using System;
using CID;
using Repo;

namespace BlobStore;

public class S3BlobStore(
    string did,
    string bucket,
    string region,
    string? endpoint,
    bool forcePathStyle,
    string accessKeyId,
    string secretAccessKey,
    TimeSpan uploadTimeout
) : IBlobStore
{
    public Task Delete(Cid cid)
    {
        throw new NotImplementedException();
    }

    public Task DeleteMany(Cid[] cid)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> GetBytes(Cid cid)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStream(Cid cid)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetTempStream(string key)
    {
        throw new NotImplementedException();
    }

    public Task MakePermanent(string tmpKey, Cid cid)
    {
        throw new NotImplementedException();
    }

    public Task PutPermanent(Cid cid, byte[] bytes)
    {
        throw new NotImplementedException();
    }

    public Task PutPermanent(Cid cid, Stream stream)
    {
        throw new NotImplementedException();
    }

    public Task<string> PutTemp(byte[] bytes)
    {
        throw new NotImplementedException();
    }

    public Task<string> PutTemp(byte[] bytes, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<string> PutTemp(Stream stream)
    {
        throw new NotImplementedException();
    }

    public Task<string> PutTemp(Stream stream, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
