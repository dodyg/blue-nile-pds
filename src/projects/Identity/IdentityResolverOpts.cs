namespace Identity;

public class IdentityResolverOpts
{
    public required int TimeoutMs { get; init; }
    public required string PlcUrl { get; init; }

    public required IDidCache DidCache { get; init; }
    // public string[] BackupNameServers { get; set; }
}