using System;
using ActorStore.Db;
using CID;
using Microsoft.EntityFrameworkCore;
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


    public async Task<Db.Blob?> GetBlobAsync(Cid cid)
    {
        var blob = Db.Blobs.FirstOrDefault(b => b.Cid == cid.ToString());
        return blob;
    }
    public async Task<List<string>> GetRecordsForBlobAsync(Cid cid)
    {
        var recordUris = await Db.RecordBlobs
            .Where(rb => rb.BlobCid == cid.ToString())
            .Select(rb => rb.RecordUri)
            .AsNoTracking()
            .ToListAsync();
        return recordUris;
    }
    public async Task<List<string>> GetRecordsForBlobAsync(string cid)
    {
        var recordUris = await Db.RecordBlobs
            .Where(rb => rb.BlobCid == cid.ToString())
            .Select(rb => rb.RecordUri)
            .AsNoTracking()
            .ToListAsync();
        return recordUris;
    }

    public async Task<List<string>> ListBlobsAsync(string? since, string? cursor, int limit)
    {
        var query = Db.RecordBlobs
            .AsNoTracking()
            .Select(x => x.BlobCid)
            .OrderBy(x => x)
            .Distinct()
            .AsQueryable();

        if (!string.IsNullOrEmpty(since))
        {
            query = Db.RecordBlobs
                .AsNoTracking()
                .Join(Db.Records,
                    rb => rb.RecordUri,
                    r => r.Uri,
                    (rb, r) => new { rb, r })
                .Where(x => string.Compare(x.r.RepoRev, since) > 0)
                .Select(x => x.rb.BlobCid)
                .OrderBy(x => x)
                .Distinct()
                .AsQueryable();
        }

        if (!string.IsNullOrEmpty(cursor))
        {
            query = query.Where(x => string.Compare(x, cursor) > 0);
        }

        var res = await query.Take(limit).ToListAsync();
        return res;
    }

    public async Task<BlobMetaData> GenerateTempBlobMetadataAsync(string tempKey, string userMimeType)
    {
        var stream = await BlobStore.GetTempStreamAsync(tempKey);

        
        var cid = await CID.Util.CidForBlobsAsync(stream);

        // let the blob store handle figuring out the size
        // don't try to read the stream length here as it might not be seekable, so it will throw not supported exception
        var size = await BlobStore.GetTempSizeAsync(tempKey);
        // TODO: content type sniffing 

        var blob = new BlobMetaData(
            MimeType: string.IsNullOrEmpty(userMimeType) ? "application/octet-stream" : userMimeType,
            Size: size,
            Cid: cid
        );
        return blob;
    }

    public async Task SaveBlobRecordAsync(Db.Blob blob)
    {
        await Db.Blobs.AddAsync(blob);
        await Db.SaveChangesAsync();
    }

    public async Task UpdateBlobAsync(Cid cid, Action<Db.Blob> updateAction)
    {
        var blob = await GetBlobAsync(cid);
        if (blob is not null)
        {
            updateAction(blob);
            await Db.SaveChangesAsync();
        }
    }

    public async Task ProcessWriteBlobsAsync(string rev, IPreparedWrite[] preparedWrites)
    {
        await using var tx = await Db.Database.BeginTransactionAsync();

        HashSet<string> newBlobCids = preparedWrites.Where(pw => pw is PreparedCreate or PreparedUpdate)
            .Cast<IPreparedDataWrite>()
            .SelectMany(pw => pw.Blobs)
            .Select(b => b.Cid.ToString())
            .ToHashSet();
        
        var existingBlobs = await Db.Blobs
            .Where(b => newBlobCids.Contains(b.Cid))
            .Select(b => b.Cid)
            .Distinct()
            .ToHashSetAsync();
        
        var difference = newBlobCids.Except(existingBlobs);

        if (difference.Any())
            throw new InvalidOperationException($"Attempting to write records with blobs that have not been uploaded {string.Join(", ", difference.Select(d => d))}, upload blobs before writing records.");

        var cidsToDelete = await DeleteDereferencedBlobsAsync(preparedWrites);

        var newBlobReferences = preparedWrites
            .Where(pw => pw is PreparedCreate or PreparedUpdate)
            .Cast<IPreparedDataWrite>()
            .SelectMany(pw => pw.Blobs.Select(b => new RecordBlob
            {
                RecordUri = pw.Uri.ToString(),
                BlobCid = b.Cid.ToString(),
            }))
            .ToArray();

        if (newBlobReferences.Length > 0)
        {
            await Db.RecordBlobs.AddRangeAsync(newBlobReferences);
        }

        var toMakePermanent = await Db.Blobs
            .Where(b => newBlobCids.Contains(b.Cid) && b.Status == BlobStatus.Temporary)
            .ToArrayAsync();

        // make permanent in db
        await Db.Blobs
            .Where(b => newBlobCids.Contains(b.Cid) && b.Status == BlobStatus.Temporary)
            .ExecuteUpdateAsync(b => b.SetProperty(b => b.Status, BlobStatus.Permanent));
    
        await Db.SaveChangesAsync();
        await tx.CommitAsync();


        // now make changes to the blob store 
        // what if blob store operation fails? it might get out of sync with db

        // do sequentially for now
        await BlobStore.DeleteManyAsync(cidsToDelete.Select(cid => Cid.FromString(cid)).ToArray());

        foreach (var blob in toMakePermanent)
        {
            // TODO: add blob validation (verifyBlob method)
            await BlobStore.MakePermanentAsync(blob.TempKey!, Cid.FromString(blob.Cid));
        }


    }

    async Task<HashSet<string>> DeleteDereferencedBlobsAsync(IPreparedWrite[] preparedWrites)
    {
        var deletes = preparedWrites.Where(pw => pw is PreparedDelete).ToArray();
        var updates = preparedWrites.Where(pw => pw is PreparedUpdate).ToArray();

        string[] uris = [..deletes.Select(d => d.Uri.ToString()), ..updates.Select(u => u.Uri.ToString())];

        if (uris.Length == 0)
            return [];

        var deletedRepoBlobs = await Db.RecordBlobs.Where(rb => uris.Contains(rb.RecordUri))
            .Select(rb => rb.BlobCid).AsNoTracking().ToArrayAsync();

        if (deletedRepoBlobs.Length == 0)
            return [];

        await Db.RecordBlobs.Where(rb => uris.Contains(rb.RecordUri))
            .ExecuteDeleteAsync();


        var dupeCids = await Db.RecordBlobs
            .Where(rb => deletedRepoBlobs.Contains(rb.BlobCid))
            .Select(rb => rb.BlobCid)
            .Distinct()
            .ToArrayAsync();

        var newBlobCids = preparedWrites
            .Where(w => w is PreparedCreate or PreparedUpdate)
            .Cast<IPreparedDataWrite>()
            .SelectMany(w => w.Blobs)
            .Select(b => b.Cid.ToString())
            .ToHashSet();

        var cidsToKeep = newBlobCids.Union(dupeCids).ToHashSet();

        var cidsToDelete = deletedRepoBlobs.Except(cidsToKeep).ToHashSet();

        // maybe delete temp in garbage collection instead
        await Db.Blobs
            .Where(b => cidsToDelete.Contains(b.Cid) && b.Status == BlobStatus.Permanent)
            .ExecuteDeleteAsync();

        return cidsToDelete;
    }



    public record BlobMetaData(
        string MimeType,
        long Size,
        Cid Cid
    );
}
