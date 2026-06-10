namespace Identity;

public class IdentityResolverOpts
{
    public required int TimeoutMs { get; init; }
    public required string PlcUrl { get; init; }

    public required IDidCache DidCache { get; init; }
    public List<string> BackupNameservers { get; set; } = [];
}