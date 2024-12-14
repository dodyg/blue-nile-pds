namespace Config;

public record DatabaseConfig
{
    public string AccountDbLoc { get; init; }
    public string SequencerDbLoc { get; init; }
    public string DidCacheDbLoc { get; init; }
    public bool DisableWalAutoCheckpoint { get; init; }
}