using System.Collections.Concurrent;
using CommonWeb;

namespace Identity;


public class MemoryCache : IDidCache
{
    private record CacheVal(DidDocument Doc, DateTime UpdatedAt);
    
    private readonly TimeSpan _staleTtl;
    private readonly TimeSpan _maxTtl;
    private readonly ConcurrentDictionary<string, CacheVal> _cache = new();
    public MemoryCache(TimeSpan? staleTtl = null, TimeSpan? maxTtl = null)
    {
        _staleTtl = staleTtl ?? TimeSpan.FromHours(1);
        _maxTtl = maxTtl ?? TimeSpan.FromDays(1);
    }

    public Task CacheDid(string did, DidDocument doc, CacheResult? prevResult = null)
    {
        _cache[did] = new CacheVal(doc, DateTime.UtcNow);
        return Task.CompletedTask;
    }
    
    public Task<CacheResult?> CheckCache(string did)
    {
        if (!_cache.TryGetValue(did, out var val))
        {
            return Task.FromResult<CacheResult?>(null);
        }
        var now = DateTime.UtcNow;
        var expired = now > val.UpdatedAt + _maxTtl;
        var stale = now > val.UpdatedAt + _staleTtl;
        return Task.FromResult<CacheResult?>(new CacheResult
        {
            Doc = val.Doc,
            Did = did,
            Expired = expired,
            Stale = stale,
            UpdatedAt = val.UpdatedAt
        });
    }
    
    public async Task RefreshCache(string did, Func<Task<DidDocument?>> getDoc, CacheResult? prevResult = null)
    {
        var doc = await getDoc();
        if (doc == null)
        {
            return;
        }
        await CacheDid(did, doc);
    }
    
    public Task ClearEntry(string did)
    {
        _cache.TryRemove(did, out _);
        return Task.CompletedTask;
    }
    
    public Task Clear()
    {
        _cache.Clear();
        return Task.CompletedTask;
    }
}