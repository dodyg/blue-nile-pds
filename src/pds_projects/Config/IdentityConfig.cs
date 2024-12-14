namespace Config;

public record IdentityConfig
{
    public string PlcUrl { get; init; }
    public int ResolverTimeout { get; init; }
    public int CacheStaleTTL { get; init; }
    public int CacheMaxTTL { get; init; }
    public string? RecoveryDidKey { get; init; }
    public List<string> ServiceHandleDomains { get; init; }
    public bool EnableDidDocWithSession { get; init; }
}