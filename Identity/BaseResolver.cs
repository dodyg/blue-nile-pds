using System.Text.Json;
using CommonWeb;

namespace Identity;

public abstract class BaseResolver
{
    protected IDidCache? Cache { get; }
    
    public BaseResolver(IDidCache? cache = null)
    {
        Cache = cache;
    }
    
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
            throw new PoorlyFormattedDidDocumentError(did,val, null);
        }
        
        return doc;
    }
    
    public abstract Task<string?> ResolveNoCheck(string did);
    
    public async Task<DidDocument?> ResolveNoCache(string did)
    {
        var doc = await ResolveNoCheck(did);
        if (doc == null)
        {
            return null;
        }
        return ValidateDidDoc(did, doc);
    }

    public async Task RefreshCache(string did, CacheResult? prevResult = null)
    {
        if (Cache == null)
        {
            return;
        }
        
        await Cache.RefreshCache(did, () => ResolveNoCache(did), prevResult);
    }

    public async Task<DidDocument?> Resolve(string did, bool forceRefresh = false)
    {
        CacheResult? fromCache = null;
        if (Cache != null && !forceRefresh)
        {
            fromCache = await Cache.CheckCache(did);
            if (fromCache != null && fromCache.Expired == false)
            {
                if (fromCache.Stale)
                {
                    await RefreshCache(did, fromCache);
                }

                return fromCache.Doc;
            }
        }
        
        var got = await ResolveNoCache(did);
        if (got == null)
        {
            if (Cache != null)
            {
                await Cache.ClearEntry(did);
            }
            return null;
        }

        if (Cache != null)
        {
            await Cache.CacheDid(did, got, fromCache ?? null);
        }
        
        return got;
    }
    
    public async Task<DidDocument> EnsureResolve(string did, bool forceRefresh = false)
    {
        var doc = await Resolve(did, forceRefresh);
        if (doc == null)
        {
            throw new DidNotFoundError(did);
        }
        return doc;
    }
    
    public async Task<AtprotoData> ResolveAtproto(string did, bool forceRefresh = false)
    {
        var doc = await EnsureResolve(did, forceRefresh);
        return Atproto_Data.EnsureAtpDocument(doc);
    }
    
    public async Task<string> ResolveAtprotoKey(string did, bool forceRefresh = false)
    {
        var doc = await EnsureResolve(did, forceRefresh);
        return Atproto_Data.EnsureAtprotoKey(doc);
    }

    public async Task<bool> VerifySignature(string did, byte[] data, byte[] sig, bool forceRefresh = false)
    {
        var signingKey = await ResolveAtprotoKey(did, forceRefresh);
        return Crypto.Verify.VerifySignature(signingKey, data, sig, null, null);
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
