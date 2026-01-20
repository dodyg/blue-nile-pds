namespace Config;

public record BlobStoreConfig {}
public record DiskBlobstoreConfig : BlobStoreConfig
{
    public string Provider => "disk";
    public string Location { get; init; }
    public string? TempLocation { get; init; }
}

public record S3BlobstoreConfig : BlobStoreConfig
{
    public string Provider => "s3";
    public required string Bucket { get; init; }
    public required string? Region { get; init; }
    public required string? Endpoint { get; init; }
    public required bool ForcePathStyle { get; init; }
    public required string AccessKeyId { get; init; }
    public required string SecretAccessKey { get; init; }
    public required int UploadTimeoutMs { get; init; }
}
