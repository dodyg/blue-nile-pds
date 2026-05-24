using ActorStore;
using AccountManager;
using Microsoft.EntityFrameworkCore;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Repo;

public static class ListMissingBlobsEndpoints
{
    public static RouteGroupBuilder MapListMissingBlobsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.repo.listMissingBlobs", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        int limit = 500,
        string? cursor = null)
    {
        if (limit < 1 || limit > 1000) limit = 500;

        var did = context.Request.Query["did"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));

        if (!actorRepositoryProvider.Exists(did))
            throw new XRPCError(new InvalidRequestErrorDetail("Repo not found"));

        await using var actorStore = actorRepositoryProvider.Open(did);

        var result = await actorStore.TransactDbAsync(async db =>
        {
            var recordBlobs = db.RecordBlobs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(cursor))
                recordBlobs = recordBlobs.Where(rb => string.Compare(rb.BlobCid, cursor) > 0);

            var blobCids = await recordBlobs
                .OrderBy(rb => rb.BlobCid)
                .Select(rb => rb.BlobCid)
                .Distinct()
                .Take(limit + 1)
                .ToListAsync();

            var existingBlobCids = await db.Blobs
                .Where(b => blobCids.Contains(b.Cid))
                .Select(b => b.Cid)
                .ToHashSetAsync();

            var missing = blobCids.Where(cid => !existingBlobCids.Contains(cid)).Take(limit).ToList();
            var hasMore = blobCids.Count > limit;
            string? nextCursor = hasMore ? missing.LastOrDefault() : null;

            return new ListMissingBlobsOutput
            {
                Blobs = missing.Select(cid => new MissingBlob(cid)).ToList(),
                Cursor = nextCursor
            };
        });

        return Results.Ok(result);
    }
}

public class ListMissingBlobsOutput
{
    public List<MissingBlob> Blobs { get; set; } = [];
    public string? Cursor { get; set; }
}

public record MissingBlob(string Cid);
