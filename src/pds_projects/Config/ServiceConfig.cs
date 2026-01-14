namespace Config;

public record ServiceConfig
{
    public required int Port { get; init; }
    public required string Hostname { get; init; }
    public required string PublicUrl { get; init; }
    public required string Did { get; init; }
    public string? Version { get; init; }
    public required long BlobUploadLimitInBytes { get; init; }
    public required bool DevMode { get; init; }
}