using CommonWeb;

namespace Identity;

public record AtprotoData(string Did, string SigningKey, string Handle, string Pds);

public interface IDidCache
{
    public Task CacheDid(string did, DidDocument doc, CacheResult? prevResult = null);
    public Task<CacheResult?> CheckCache(string did);
    public Task RefreshCache(string did, Func<Task<DidDocument?>> getDoc, CacheResult? prevResult = null);
    public Task ClearEntry(string did);
    public Task Clear();
}

public class CacheResult
{
    public required string Did { get; init; }
    public required DidDocument Doc { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool Stale { get; set; } = false;
    public bool Expired { get; set; } = false;
}