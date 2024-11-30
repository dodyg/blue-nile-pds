// ReSharper disable InconsistentNaming
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace atompds.Pds.Config;

public class ServerEnvironment
{
    // Service Configuration
    public string PDS_SERVICE_NAME { get; set; } = "AtomPDS";
    public int PDS_PORT { get; set; } = 2583;
    public string PDS_HOSTNAME { get; set; } = "localhost";
    public string? PDS_SERVICE_DID { get; set; }
    public string? PDS_VERSION { get; set; }
    public long PDS_BLOB_UPLOAD_LIMIT { get; set; } = 5 * 1024 * 1024; // 5MB
    public bool PDS_DEV_MODE { get; set; } = false;
    
    public ServiceConfig ServiceConfig => new()
    {
        Port = PDS_PORT,
        Hostname = PDS_HOSTNAME,
        PublicUrl = PDS_HOSTNAME == "localhost" ? $"http://localhost:{PDS_PORT}" : $"https://{PDS_HOSTNAME}",
        Did = PDS_SERVICE_DID ?? $"did:web:{PDS_HOSTNAME}",
        Version = PDS_VERSION,
        BlobUploadLimit = PDS_BLOB_UPLOAD_LIMIT,
        DevMode = PDS_DEV_MODE
    };

    // Data Directories
    private string DbLoc(string name) =>
        !string.IsNullOrEmpty(PDS_DATA_DIRECTORY)
            ? Path.Combine(PDS_DATA_DIRECTORY, name)
            : name;
    
    public string? PDS_DATA_DIRECTORY { get; set; }
    public string PDS_ACCOUNT_DB_LOCATION { get; set; } = "account.sqlite";
    public string PDS_SEQUENCER_DB_LOCATION { get; set; } = "sequencer.sqlite";
    public string PDS_DID_CACHE_DB_LOCATION { get; set; } = "did_cache.sqlite";
    
    public DatabaseConfig DatabaseConfig => new()
    {
        AccountDbLoc = DbLoc(PDS_ACCOUNT_DB_LOCATION),
        SequencerDbLoc = DbLoc(PDS_SEQUENCER_DB_LOCATION),
        DidCacheDbLoc = DbLoc(PDS_DID_CACHE_DB_LOCATION)
    };

    // Actor Store
    public string PDS_ACTOR_STORE_DIRECTORY { get; set; } = "actors";
    public long PDS_ACTOR_SCORE_CACHE_SIZE { get; set; } = 100;
    
    public ActorStoreConfig ActorStoreConfig => new()
    {
        Directory = DbLoc(PDS_ACTOR_STORE_DIRECTORY),
        CacheSize = PDS_ACTOR_SCORE_CACHE_SIZE
    };

    // Blobstore
    public required string PDS_BLOBSTORE_DISK_LOCATION { get; set; }
    public required string PDS_BLOBSTORE_DISK_TMP_LOCATION { get; set; }
    
    public DiskBlobstoreConfig DiskBlobstoreConfig => new DiskBlobstoreConfig
    {
        Location = PDS_BLOBSTORE_DISK_LOCATION,
        TempLocation = PDS_BLOBSTORE_DISK_TMP_LOCATION
    };

    // Identity
    private const int SECOND = 1000;
    private const int MINUTE = 60 * SECOND;
    private const int HOUR = 60 * MINUTE;
    private const int DAY = 24 * HOUR;
    public string PDS_DID_PLC_URL { get; set; } = "https://plc.directory";
    public int PDS_DID_CACHE_STALE_TTL { get; set; } = DAY;
    public int PDS_DID_CACHE_MAX_TTL { get; set; } = HOUR;
    public int PDS_ID_RESOLVER_TIMEOUT { get; set; }  = 3 * SECOND;
    public string? PDS_RECOVERY_DID_KEY { get; set; }
    public List<string> PDS_SERVICE_HANDLE_DOMAINS { get; set; } = [];
    public bool PDS_ENABLE_DID_DOC_WITH_SESSION { get; set; } = false;
    
    public IdentityConfig IdentityConfig => new()
    {
        PlcUrl = PDS_DID_PLC_URL,
        CacheMaxTTL = PDS_DID_CACHE_STALE_TTL,
        CacheStaleTTL = PDS_DID_CACHE_MAX_TTL,
        ResolverTimeout = PDS_ID_RESOLVER_TIMEOUT,
        RecoveryDidKey = PDS_RECOVERY_DID_KEY,
        ServiceHandleDomains = PDS_SERVICE_HANDLE_DOMAINS?.Count > 0 ? PDS_SERVICE_HANDLE_DOMAINS : 
            PDS_HOSTNAME == "localhost" ? new List<string> { ".test" } : new List<string> { $".{PDS_HOSTNAME}" },
        EnableDidDocWithSession = PDS_ENABLE_DID_DOC_WITH_SESSION
    };
    

    // Invites
    public bool PDS_INVITE_REQUIRED { get; set; } = false;
    public int? PDS_INVITE_INTERVAL { get; set; }
    public int InviteEpoch { get; set; } = 0;
    
    public InvitesConfig InviteConfig => PDS_INVITE_REQUIRED ? 
        new RequiredInvitesConfig
        {
            Interval = PDS_INVITE_INTERVAL,
            Epoch = InviteEpoch
        } :
        new NonRequiredInvitesConfig();
    
    // Subscription
    public long PDS_MAX_SUBSCRIPTION_BUFFER { get; set; } = 500;
    public long PDS_REPO_BACKFILL_LIMIT_MS { get; set; } = DAY;

    // Bsky App View
    public string? PDS_BSKY_APP_VIEW_URL { get; set; }
    public string? PDS_BSKY_APP_VIEW_DID { get; set; }
    public string? PDS_BSKY_APP_VIEW_CDN_URL_PATTERN { get; set; }
    
    public IBskyAppViewConfig BskyAppViewConfig => PDS_BSKY_APP_VIEW_URL != null ? 
        new BskyAppViewConfig
        {
            Url = PDS_BSKY_APP_VIEW_URL,
            Did = PDS_BSKY_APP_VIEW_DID ?? throw new Exception("PDS_BSKY_APP_VIEW_URL is set but PDS_BSKY_APP_VIEW_DID is not"),
            CdnUrlPattern = PDS_BSKY_APP_VIEW_CDN_URL_PATTERN
        } : new DisabledBskyAppViewConfig();

    // Crawlers
    public List<string> PDS_CRAWLERS { get; set; } = [];

    // Secrets
    public required string PDS_JWT_SECRET { get; set; }
    
    // Keys
    public required string PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX { get; set; }
    
    
    public SecretsConfig SecretsConfig => new()
    {
        JwtSecret = PDS_JWT_SECRET,
        PlcRotationKey = Crypto.Secp256k1.Secp256k1Keypair.Import(PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX, false)
    };
    
    // Fetch
    public bool? PDS_DISABLE_SSRF_PROTECTION { get; set; }
    public long? PDS_FETCH_MAX_RESPONSE_SIZE { get; set; }

    // Proxy
    public bool? PDS_PROXY_ALLOW_HTTP2 { get; set; }
    public int? PDS_PROXY_HEADERS_TIMEOUT { get; set; }
    public int? PDS_PROXY_BODY_TIMEOUT { get; set; }
    public long? PDS_PROXY_MAX_RESPONSE_SIZE { get; set; }
    public int? PDS_PROXY_MAX_RETRIES { get; set; }
    public bool? PDS_PROXY_PREFER_COMPRESSED { get; set; }
    
    public ProxyConfig ProxyConfig => new()
    {
        DisableSsrfProtection = PDS_DISABLE_SSRF_PROTECTION ?? PDS_DEV_MODE,
        AllowHTTP2 = PDS_PROXY_ALLOW_HTTP2 ?? false,
        HeadersTimeout = PDS_PROXY_HEADERS_TIMEOUT ?? 10000,
        BodyTimeout = PDS_PROXY_BODY_TIMEOUT ?? 30000,
        MaxResponseSize = PDS_PROXY_MAX_RESPONSE_SIZE ?? (10 * 1024 * 1024), // 10MB
        MaxRetries = PDS_PROXY_MAX_RETRIES is > 0 ? PDS_PROXY_MAX_RETRIES.Value : 3,
        PreferCompressed = PDS_PROXY_PREFER_COMPRESSED ?? false
    };
}