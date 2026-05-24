namespace Config;

public record ActorStoreConfig
{
    public required string Directory { get; init; }
    public long CacheSize { get; init; }
    public bool DisableWalAutoCheckpoint { get; init; }
}