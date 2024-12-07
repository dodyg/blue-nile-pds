namespace Config;

public record ActorStoreConfig
{
    public string Directory { get; init; }
    public long CacheSize { get; init; }
    public bool DisableWalAutoCheckpoint { get; init; }
}