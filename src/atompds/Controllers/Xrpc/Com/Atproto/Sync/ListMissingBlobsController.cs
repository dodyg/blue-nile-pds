using System.Text.Json;
using ActorStore;
using ActorStore.Db;
using AccountManager;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[ApiController]
[Route("xrpc")]
public class ListMissingBlobsController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly ILogger<ListMissingBlobsController> _logger;

    public ListMissingBlobsController(
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        ILogger<ListMissingBlobsController> logger)
    {
        _accountRepository = accountRepository;
        _actorRepositoryProvider = actorRepositoryProvider;
        _logger = logger;
    }

    [HttpGet("com.atproto.sync.listMissingBlobs")]
    public async Task<IActionResult> ListMissingBlobsAsync(
        [FromQuery] int limit = 500,
        [FromQuery] string? cursor = null)
    {
        if (limit < 1 || limit > 1000)
        {
            limit = 500;
        }

        var did = HttpContext.Request.Query["did"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        }

        if (!_actorRepositoryProvider.Exists(did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Repo not found"));
        }

        await using var actorStore = _actorRepositoryProvider.Open(did);

        var query = actorStore.TransactDbAsync(async db =>
        {
            var recordBlobs = db.RecordBlobs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(cursor))
            {
                recordBlobs = recordBlobs.Where(rb => string.Compare(rb.BlobCid, cursor) > 0);
            }

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

            var missing = blobCids
                .Where(cid => !existingBlobCids.Contains(cid))
                .Take(limit)
                .ToList();

            var hasMore = blobCids.Count > limit;
            string? nextCursor = hasMore ? missing.LastOrDefault() : null;

            return new ListMissingBlobsOutput
            {
                Blobs = missing.Select(cid => new MissingBlob(cid)).ToList(),
                Cursor = nextCursor
            };
        });

        return Ok(await query);
    }
}

public class ListMissingBlobsOutput
{
    public List<MissingBlob> Blobs { get; set; } = [];
    public string? Cursor { get; set; }
}

public record MissingBlob(string Cid);
