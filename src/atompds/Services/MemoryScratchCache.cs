using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace atompds.Services;

public interface IScratchCache
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan? ttl = null);
    Task DeleteAsync(string key);
}

public class MemoryScratchCache : IScratchCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<MemoryScratchCache> _logger;

    public MemoryScratchCache(ILogger<MemoryScratchCache> logger)
    {
        _logger = logger;
    }

    public Task<string?> GetAsync(string key)
    {
        CleanupIfNeeded();
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt == null || entry.ExpiresAt > DateTime.UtcNow)
            {
                return Task.FromResult<string?>(entry.Value);
            }

            _cache.TryRemove(key, out _);
        }

        return Task.FromResult<string?>(null);
    }

    public Task SetAsync(string key, string value, TimeSpan? ttl = null)
    {
        var entry = new CacheEntry
        {
            Value = value,
            ExpiresAt = ttl.HasValue ? DateTime.UtcNow + ttl.Value : null
        };
        _cache[key] = entry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private void CleanupIfNeeded()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt.Value <= now)
            {
                _cache.TryRemove(kvp.Key, out _);
            }
        }
    }

    private record CacheEntry
    {
        public string Value { get; init; } = "";
        public DateTime? ExpiresAt { get; init; }
    }
}
