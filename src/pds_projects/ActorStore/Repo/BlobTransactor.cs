using System;
using ActorStore.Db;
using CID;
using Repo;

namespace ActorStore.Repo;

public class BlobTransactor
{
    public IBlobStore BlobStore { get; }
    public ActorStoreDb Db { get; }
    public BlobTransactor(IBlobStore blobStore, ActorStoreDb db)
    {
        BlobStore = blobStore;
        Db = db;
    }


    public async Task<Blob?> GetBlob(Cid cid)
    {
        var blob = Db.Blobs.FirstOrDefault(b => b.Cid == cid.ToString());
        return blob;
    }

    public async Task<BlobMetaData> GenerateTempBlobMetadata(string tempKey, string userMimeType)
    {
        var stream = await BlobStore.GetTempStream(tempKey);

        var cid = await CID.Util.CidForBlobs(stream);
        var size = (int)stream.Length;
        // TODO: content type sniffing 

        var blob = new BlobMetaData(
            MimeType: string.IsNullOrEmpty(userMimeType) ? "application/octet-stream" : userMimeType,
            Size: size,
            Cid: cid
        );
        return blob;
    }

    public async Task SaveBlobRecord(Blob blob)
    {
        await Db.Blobs.AddAsync(blob);
        await Db.SaveChangesAsync();
    }

    public async Task UpdateBlob(Cid cid, Action<Blob> updateAction)
    {
        var blob = await GetBlob(cid);
        if (blob != null)
        {
            updateAction(blob);
            await Db.SaveChangesAsync();
        }
    }


    public record BlobMetaData(
        string MimeType,
        int Size,
        Cid Cid
    );
}
