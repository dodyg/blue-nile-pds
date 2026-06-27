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

        // T-12: content type sniffing
        var sniffedMime = await SniffMimeTypeAsync(tempKey);
        if (!string.IsNullOrEmpty(sniffedMime) && !string.IsNullOrEmpty(userMimeType))
        {
            var highRiskTypes = new[] { "text/html", "image/svg+xml", "application/javascript", "text/javascript" };
            var isHighRisk = highRiskTypes.Any(ht =>
                userMimeType.Contains(ht, StringComparison.OrdinalIgnoreCase) ||
                sniffedMime.Contains(ht, StringComparison.OrdinalIgnoreCase));
            if (isHighRisk && !userMimeType.Equals(sniffedMime, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"MIME type mismatch: declared {userMimeType}, sniffed {sniffedMime}");
            }
        }

        var blob = new BlobMetaData(
            MimeType: string.IsNullOrEmpty(userMimeType) ? (sniffedMime ?? "application/octet-stream") : userMimeType,
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
            .DistinctBy(rb => new { rb.BlobCid, rb.RecordUri })
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
            .AsNoTracking()
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



    private async Task<string?> SniffMimeTypeAsync(string tempKey)
    {
        try
        {
            var stream = await BlobStore.GetTempStreamAsync(tempKey);
            var buffer = new byte[512];
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0) return null;

            var span = buffer.AsSpan(0, read);

            // SVG
            if (span.Length > 5 &&
                (span[0] == 0x3C && span[1] == 0x73 && span[2] == 0x76 && span[3] == 0x67) ||
                (span[0] == 0x3C && span[1] == 0x3F && span[2] == 0x78 && span[3] == 0x6D && span[4] == 0x6C))
            {
                return "image/svg+xml";
            }

            // HTML
            if (span.Length > 6 &&
                (span[0] == 0x3C && span[1] == 0x21 && span[2] == 0x44 && span[3] == 0x4F && span[4] == 0x43 && span[5] == 0x54) ||
                (span[0] == 0x3C && span[1] == 0x68 && span[2] == 0x74 && span[3] == 0x6D && span[4] == 0x6C) ||
                (span[0] == 0x3C && span[1] == 0x48 && span[2] == 0x54 && span[3] == 0x4D && span[4] == 0x4C))
            {
                return "text/html";
            }

            // JavaScript
            if (span.Length > 10)
            {
                var text = System.Text.Encoding.UTF8.GetString(span);
                if (text.Contains("function", StringComparison.Ordinal) ||
                    text.Contains("var ", StringComparison.Ordinal) ||
                    text.Contains("const ", StringComparison.Ordinal) ||
                    text.Contains("let ", StringComparison.Ordinal))
                {
                    return "application/javascript";
                }
            }

            // PNG
            if (span.Length > 8 && span[0] == 0x89 && span[1] == 0x50 && span[2] == 0x4E && span[3] == 0x47)
                return "image/png";

            // JPEG
            if (span.Length > 3 && span[0] == 0xFF && span[1] == 0xD8)
                return "image/jpeg";

            // GIF
            if (span.Length > 6 && span[0] == 0x47 && span[1] == 0x49 && span[2] == 0x46)
                return "image/gif";

            // WebP
            if (span.Length > 12 && span[0] == 0x52 && span[1] == 0x49 && span[2] == 0x46 && span[3] == 0x46 &&
                span[8] == 0x57 && span[9] == 0x45 && span[10] == 0x42 && span[11] == 0x50)
                return "image/webp";

            // MP4
            if (span.Length > 12 && span[4] == 0x66 && span[5] == 0x74 && span[6] == 0x79 && span[7] == 0x70)
                return "video/mp4";

            return null;
        }
        catch
        {
            return null;
        }
    }

    public record BlobMetaData(
        string MimeType,
        long Size,
        Cid Cid
    );
}
