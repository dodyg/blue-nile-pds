namespace Config;

public record DatabaseConfig
{
    public required string AccountDbLoc { get; init; }
    public required string SequencerDbLoc { get; init; }
    public required string DidCacheDbLoc { get; init; }
    public bool DisableWalAutoCheckpoint { get; init; }
}