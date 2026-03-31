using System.Text.Json;
using CommonWeb;
using Crypto;

namespace Identity;

public abstract class BaseResolver
{

    public BaseResolver(IDidCache? cache = null)
    {
        Cache = cache;
    }
    protected IDidCache? Cache { get; }

    public DidDocument ValidateDidDoc(string did, string val)
    {
        DidDocument doc;
        try
        {
            doc = JsonSerializer.Deserialize<DidDocument>(val) ?? throw new PoorlyFormattedDidDocumentError(did, val, null);
        }
        catch (Exception e)
        {
            throw new PoorlyFormattedDidDocumentError(did, val, e);
        }

        if (doc.Id != did)
        {
            throw new PoorlyFormattedDidDocumentError(did, val, null);
        }

        return doc;
    }

    public abstract Task<string?> ResolveNoCheckAsync(string did);

    public async Task<DidDocument?> ResolveNoCacheAsync(string did)
    {
        var doc = await ResolveNoCheckAsync(did);
        if (doc == null)
        {
            return null;
        }
        return ValidateDidDoc(did, doc);
    }

    public async Task RefreshCacheAsync(string did, CacheResult? prevResult = null)
    {
        if (Cache == null)
        {
            return;
        }

        await Cache.RefreshCacheAsync(did, () => ResolveNoCacheAsync(did), prevResult);
    }

    public async Task<DidDocument?> ResolveAsync(string did, bool forceRefresh = false)
    {
        CacheResult? fromCache = null;
        if (Cache != null && !forceRefresh)
        {
            fromCache = await Cache.CheckCacheAsync(did);
            if (fromCache != null && fromCache.Expired == false)
            {
                if (fromCache.Stale)
                {
                    await RefreshCacheAsync(did, fromCache);
                }

                return fromCache.Doc;
            }
        }

        var got = await ResolveNoCacheAsync(did);
        if (got == null)
        {
            if (Cache != null)
            {
                await Cache.ClearEntryAsync(did);
            }
            return null;
        }

        if (Cache != null)
        {
            await Cache.CacheDidAsync(did, got, fromCache ?? null);
        }

        return got;
    }

    public async Task<DidDocument> EnsureResolveAsync(string did, bool forceRefresh = false)
    {
        var doc = await ResolveAsync(did, forceRefresh);
        if (doc == null)
        {
            throw new DidNotFoundError(did);
        }
        return doc;
    }

    public async Task<AtprotoData> ResolveAtprotoAsync(string did, bool forceRefresh = false)
    {
        var doc = await EnsureResolveAsync(did, forceRefresh);
        return Atproto_Data.EnsureAtpDocument(doc);
    }

    public async Task<string> ResolveAtprotoKeyAsync(string did, bool forceRefresh = false)
    {
        var doc = await EnsureResolveAsync(did, forceRefresh);
        return Atproto_Data.EnsureAtprotoKey(doc);
    }

    public async Task<bool> VerifySignatureAsync(string did, byte[] data, byte[] sig, bool forceRefresh = false)
    {
        var signingKey = await ResolveAtprotoKeyAsync(did, forceRefresh);
        return Verify.VerifySignature(signingKey, data, sig, null, null);
    }
}

public class DidNotFoundError : Exception
{
    public DidNotFoundError(string did) : base($"Could not resolve DID: ${did}")
    {
        Did = did;
    }
    public string Did { get; }
}

public class PoorlyFormattedDidDocumentError : Exception
{
    public PoorlyFormattedDidDocumentError(string did, string? doc, Exception? innerException) : base($"Poorly formatted DID Document: ${doc}", innerException)
    {
        Doc = doc;
    }
    public string? Doc { get; }
}