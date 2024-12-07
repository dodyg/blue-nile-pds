namespace Config;

public record DiskBlobstoreConfig
{
    public string Provider => "disk";
    public string Location { get; init; }
    public string? TempLocation { get; init; }
}