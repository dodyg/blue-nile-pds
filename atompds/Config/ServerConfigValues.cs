namespace atompds.Config;

using System.Collections.Generic;

public record ServiceConfig
{
    public required int Port { get; init; }
    public required string Hostname { get; init; }
    public required string PublicUrl { get; init; }
    public required string Did { get; init; }
    public string? Version { get; init; }
    public required long BlobUploadLimit { get; init; }
    public required bool DevMode { get; init; }
}

public record DatabaseConfig
{
    public string AccountDbLoc { get; init; }
    public string SequencerDbLoc { get; init; }
    public string DidCacheDbLoc { get; init; }
    public bool DisableWalAutoCheckpoint { get; init; }
}

public record ActorStoreConfig
{
    public string Directory { get; init; }
    public long CacheSize { get; init; }
    public bool DisableWalAutoCheckpoint { get; init; }
}

public record DiskBlobstoreConfig
{
    public string Provider => "disk";
    public string Location { get; init; }
    public string? TempLocation { get; init; }
}

public record IdentityConfig
{
    public string PlcUrl { get; init; }
    public int ResolverTimeout { get; init; }
    public int CacheStaleTTL { get; init; }
    public int CacheMaxTTL { get; init; }
    public string? RecoveryDidKey { get; init; }
    public List<string> ServiceHandleDomains { get; init; }
    public List<string>? HandleBackupNameservers { get; init; }
    public bool EnableDidDocWithSession { get; init; }
}

public record ProxyConfig
{
    public bool DisableSsrfProtection { get; init; }
    public bool AllowHTTP2 { get; init; }
    public int HeadersTimeout { get; init; }
    public int BodyTimeout { get; init; }
    public long MaxResponseSize { get; init; }
    public int MaxRetries { get; init; }
    public bool PreferCompressed { get; init; }
}

public abstract record InvitesConfig
{
    public abstract bool Required { get; }
}

public record RequiredInvitesConfig : InvitesConfig
{
    public override bool Required => true;
    public int? Interval { get; init; }
    public int Epoch { get; init; }
}

public record NonRequiredInvitesConfig : InvitesConfig
{
    public override bool Required => false;
}

public interface IBskyAppViewConfig;

public record DisabledBskyAppViewConfig : IBskyAppViewConfig;

public record BskyAppViewConfig : IBskyAppViewConfig
{
    public string Url { get; init; }
    public string Did { get; init; }
    public string? CdnUrlPattern { get; init; }
}

public record SecretsConfig
{
    public required string JwtSecret { get; init; }
}