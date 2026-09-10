using System.IO.Compression;
using System.Text.Json;
using BlueNilePds.Core.CID;
using BlueNilePds.Pds.AccountManager;
using BlueNilePds.Pds.ActorStore;
using BlueNilePds.Pds.BlobStore;
using BlueNilePds.Pds.Xrpc;

namespace BlueNilePds.Host.Services;

public class UserDataExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Dictionary<string, string> MimeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["video/mp4"] = ".mp4",
        ["video/quicktime"] = ".mov",
        ["video/webm"] = ".webm",
        ["audio/mpeg"] = ".mp3",
    };

    private const int RecordsPageSize = 500;
    private const int BlobPageSize = 500;

    private readonly AccountRepository _accountRepository;
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly ILogger<UserDataExportService> _logger;

    public UserDataExportService(
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        ILogger<UserDataExportService> logger)
    {
        _accountRepository = accountRepository;
        _actorRepositoryProvider = actorRepositoryProvider;
        _logger = logger;
    }

    public async Task<(string Did, string Handle)> ResolveAccountAsync(string didOrHandle)
    {
        string? did = didOrHandle.StartsWith("did:", StringComparison.Ordinal)
            ? didOrHandle
            : await _accountRepository.GetDidForActorAsync(didOrHandle);

        if (string.IsNullOrWhiteSpace(did))
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        var account = await _accountRepository.GetAccountAsync(did, new(true, true));
        if (account is null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        return (did, account.Handle);
    }

    public static string BuildFileName(string handle)
    {
        var safe = string.Concat(handle.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        return $"export-{safe}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
    }

    public async Task ExportToStreamAsync(string did, string handle, Stream output, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        var collections = await ListCollectionsAsync(did);
        var recordCount = 0;
        foreach (var collection in collections)
        {
            recordCount += await WriteCollectionAsync(did, collection, archive, cancellationToken);
        }

        var blobCount = await WriteBlobsAsync(did, archive, cancellationToken);

        var manifest = new Dictionary<string, object?>
        {
            ["did"] = did,
            ["handle"] = handle,
            ["exportedAt"] = DateTime.UtcNow.ToString("O"),
            ["collections"] = collections,
            ["recordCount"] = recordCount,
            ["blobCount"] = blobCount,
        };
        await WriteJsonEntryAsync(archive, "manifest.json", manifest, cancellationToken);
    }

    private async Task<string[]> ListCollectionsAsync(string did)
    {
        await using var actorRepo = _actorRepositoryProvider.Open(did);
        return await actorRepo.ListCollectionsAsync();
    }

    private async Task<int> WriteCollectionAsync(string did, string collection, ZipArchive archive, CancellationToken cancellationToken)
    {
        var count = 0;
        string? cursor = null;

        await using var actorRepo = _actorRepositoryProvider.Open(did);
        while (true)
        {
            var page = await actorRepo.Repo.Record.ListRecordsForCollectionAsync(collection, RecordsPageSize, reverse: false, cursor);
            if (page.Count == 0)
                break;

            foreach (var record in page)
            {
                var rkey = ExtractRkey(record.Uri);
                var entry = archive.CreateEntry($"records/{EscapeSegment(collection)}/{EscapeSegment(rkey)}.json");
                await using var entryStream = entry.Open();
                await JsonSerializer.SerializeAsync(entryStream, new Dictionary<string, object?>
                {
                    ["uri"] = record.Uri,
                    ["cid"] = record.Cid,
                    ["value"] = record.Value,
                }, JsonOptions, cancellationToken);
                count++;
            }

            if (page.Count < RecordsPageSize)
                break;

            cursor = ExtractRkey(page[^1].Uri);
        }

        return count;
    }

    private async Task<int> WriteBlobsAsync(string did, ZipArchive archive, CancellationToken cancellationToken)
    {
        var written = 0;
        var manifestEntries = new List<Dictionary<string, object?>>();
        string? cursor = null;

        await using var actorRepo = _actorRepositoryProvider.Open(did);
        while (true)
        {
            var page = await actorRepo.Repo.Blob.ListBlobsAsync(since: null, cursor, BlobPageSize);
            if (page.Count == 0)
                break;

            foreach (var blobCid in page)
            {
                var entry = await WriteBlobAsync(actorRepo, blobCid, archive, cancellationToken);
                manifestEntries.Add(entry);
                if (!entry.TryGetValue("missing", out var missing) || missing is not true)
                    written++;
            }

            if (page.Count < BlobPageSize)
                break;

            cursor = page[^1];
        }

        await WriteJsonEntryAsync(archive, "blobs/manifest.json", manifestEntries, cancellationToken);
        return written;
    }

    private async Task<Dictionary<string, object?>> WriteBlobAsync(
        ActorRepository actorRepo,
        string blobCid,
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        Cid cid;
        try
        {
            cid = Cid.FromString(blobCid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Export: skipping unparsable blob cid {Cid}", blobCid);
            return new Dictionary<string, object?>
            {
                ["cid"] = blobCid,
                ["missing"] = true,
                ["reason"] = "unparsable cid",
            };
        }

        var blob = await actorRepo.Repo.Blob.GetBlobAsync(cid);
        if (blob is null)
        {
            return new Dictionary<string, object?>
            {
                ["cid"] = blobCid,
                ["missing"] = true,
                ["reason"] = "blob record not found",
            };
        }

        if (!string.IsNullOrEmpty(blob.TakedownRef))
        {
            return new Dictionary<string, object?>
            {
                ["cid"] = blobCid,
                ["mimeType"] = blob.MimeType,
                ["size"] = blob.Size,
                ["missing"] = true,
                ["reason"] = "taken down",
            };
        }

        var fileName = $"{blobCid}{ExtensionFor(blob.MimeType)}";
        try
        {
            await using var blobStream = await actorRepo.Repo.Blob.BlobStore.GetStreamAsync(cid);
            var entry = archive.CreateEntry($"blobs/{fileName}");
            await using var entryStream = entry.Open();
            await blobStream.CopyToAsync(entryStream, cancellationToken);
        }
        catch (BlobNotFoundException)
        {
            return new Dictionary<string, object?>
            {
                ["cid"] = blobCid,
                ["mimeType"] = blob.MimeType,
                ["size"] = blob.Size,
                ["fileName"] = fileName,
                ["missing"] = true,
                ["reason"] = "blob bytes not found",
            };
        }

        var referencedBy = await actorRepo.Repo.Blob.GetRecordsForBlobAsync(blobCid);
        return new Dictionary<string, object?>
        {
            ["cid"] = blobCid,
            ["mimeType"] = blob.MimeType,
            ["size"] = blob.Size,
            ["fileName"] = fileName,
            ["referencedBy"] = referencedBy,
        };
    }

    private static async Task WriteJsonEntryAsync(ZipArchive archive, string entryName, object? value, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, value, JsonOptions, cancellationToken);
    }

    private static string ExtensionFor(string? mimeType) =>
        mimeType != null && MimeExtensions.TryGetValue(mimeType, out var ext) ? ext : ".bin";

    private static string ExtractRkey(string uri)
    {
        var lastSlash = uri.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < uri.Length - 1 ? uri[(lastSlash + 1)..] : uri;
    }

    private static string EscapeSegment(string segment) => Uri.EscapeDataString(segment);
}
