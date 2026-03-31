using CommonWeb;

namespace Identity;

public record AtprotoData(string Did, string SigningKey, string Handle, string Pds);

public interface IDidCache
{
    public Task CacheDidAsync(string did, DidDocument doc, CacheResult? prevResult = null);
    public Task<CacheResult?> CheckCacheAsync(string did);
    public Task RefreshCacheAsync(string did, Func<Task<DidDocument?>> getDoc, CacheResult? prevResult = null);
    public Task ClearEntryAsync(string did);
    public Task ClearAsync();
}

public class CacheResult
{
    public required string Did { get; init; }
    public required DidDocument Doc { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool Stale { get; set; } = false;
    public bool Expired { get; set; } = false;
}