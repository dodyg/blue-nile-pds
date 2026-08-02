namespace Config;

public record BackupConfig
{
    public required string Directory { get; init; }
    public int MaxKeep { get; init; } = 5;
}
