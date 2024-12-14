using System.ComponentModel.DataAnnotations;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Middleware;
using Config;
using DidLib;
using Handle;
using Identity;
using Mailer;
using Microsoft.EntityFrameworkCore;
using Sequencer;
using Sequencer.Db;

namespace atompds.Config;

public record ServerConfig
{
    public ServiceConfig Service { get; init; }
    public string[] Crawlers { get; init; }
    public SubscriptionConfig Subscription { get; init; }
    public DatabaseConfig Db { get; init; }
    public ActorStoreConfig ActorStore { get; init; }
    public DiskBlobstoreConfig Blobstore { get; init; }
    public IdentityConfig Identity { get; init; }
    public InvitesConfig Invites { get; init; }
    public IBskyAppViewConfig BskyAppView { get; init; }
    public ProxyConfig Proxy { get; init; }
    public SecretsConfig SecretsConfig { get; init; }

    public ServiceConfig MapServiceConfig(ServerEnvironment env) => new()
    {
        Port = env.PDS_PORT,
        Hostname = env.PDS_HOSTNAME,
        PublicUrl = env.PDS_HOSTNAME == "localhost" ? $"http://localhost:{env.PDS_PORT}" : $"https://{env.PDS_HOSTNAME}",
        Did = env.PDS_SERVICE_DID ?? $"did:web:{env.PDS_HOSTNAME}",
        Version = env.PDS_VERSION,
        BlobUploadLimit = env.PDS_BLOB_UPLOAD_LIMIT,
        DevMode = env.PDS_DEV_MODE
    };
    
    public SubscriptionConfig MapSubscriptionConfig(ServerEnvironment env) => new()
    {
        MaxSubscriptionBuffer = env.PDS_MAX_SUBSCRIPTION_BUFFER,
        RepoBackfillLimitMs = env.PDS_REPO_BACKFILL_LIMIT_MS
    };
    
    private string MapDbLoc(ServerEnvironment env, string name) =>
        !string.IsNullOrEmpty(env.PDS_DATA_DIRECTORY)
            ? Path.Combine(env.PDS_DATA_DIRECTORY, name)
            : name;
    public DatabaseConfig MapDatabaseConfig(ServerEnvironment env) => new()
    {
        AccountDbLoc = MapDbLoc(env, env.PDS_ACCOUNT_DB_LOCATION),
        SequencerDbLoc = MapDbLoc(env, env.PDS_SEQUENCER_DB_LOCATION),
        DidCacheDbLoc = MapDbLoc(env, env.PDS_DID_CACHE_DB_LOCATION)
    };
    
    public ActorStoreConfig MapActorStoreConfig(ServerEnvironment env) => new()
    {
        Directory = MapDbLoc(env, env.PDS_ACTOR_STORE_DIRECTORY),
        CacheSize = env.PDS_ACTOR_SCORE_CACHE_SIZE
    };
    
    public IdentityConfig MapIdentityConfig(ServerEnvironment env) => new()
    {
        PlcUrl = env.PDS_DID_PLC_URL,
        CacheMaxTTL = env.PDS_DID_CACHE_STALE_TTL,
        CacheStaleTTL = env.PDS_DID_CACHE_MAX_TTL,
        ResolverTimeout = env.PDS_ID_RESOLVER_TIMEOUT,
        RecoveryDidKey = env.PDS_RECOVERY_DID_KEY,
        ServiceHandleDomains = env.PDS_SERVICE_HANDLE_DOMAINS?.Count > 0 ? env.PDS_SERVICE_HANDLE_DOMAINS : 
            env.PDS_HOSTNAME == "localhost" ? new List<string> { ".test" } : new List<string> { $".{env.PDS_HOSTNAME}" },
        EnableDidDocWithSession = env.PDS_ENABLE_DID_DOC_WITH_SESSION
    };
    
    public InvitesConfig MapInviteConfig(ServerEnvironment env) => env.PDS_INVITE_REQUIRED ? 
        new RequiredInvitesConfig
        {
            Interval = env.PDS_INVITE_INTERVAL,
            Epoch = env.InviteEpoch
        } :
        new NonRequiredInvitesConfig();
    
    public IBskyAppViewConfig MapBskyAppViewConfig(ServerEnvironment env) => env.PDS_BSKY_APP_VIEW_URL != null ? 
        new BskyAppViewConfig
        {
            Url = env.PDS_BSKY_APP_VIEW_URL,
            Did = env.PDS_BSKY_APP_VIEW_DID ?? throw new Exception("PDS_BSKY_APP_VIEW_URL is set but PDS_BSKY_APP_VIEW_DID is not"),
            CdnUrlPattern = env.PDS_BSKY_APP_VIEW_CDN_URL_PATTERN
        } : new DisabledBskyAppViewConfig();
        
    
    public SecretsConfig MapSecretsConfig(ServerEnvironment env) => new()
    {
        JwtSecret = env.PDS_JWT_SECRET,
        PlcRotationKey = Crypto.Secp256k1.Secp256k1Keypair.Import(env.PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX, false)
    };
    
    public ProxyConfig MapProxyConfig(ServerEnvironment env) => new()
    {
        DisableSsrfProtection = env.PDS_DISABLE_SSRF_PROTECTION ?? env.PDS_DEV_MODE,
        AllowHTTP2 = env.PDS_PROXY_ALLOW_HTTP2 ?? false,
        HeadersTimeout = env.PDS_PROXY_HEADERS_TIMEOUT ?? 10000,
        BodyTimeout = env.PDS_PROXY_BODY_TIMEOUT ?? 30000,
        MaxResponseSize = env.PDS_PROXY_MAX_RESPONSE_SIZE ?? (10 * 1024 * 1024), // 10MB
        MaxRetries = env.PDS_PROXY_MAX_RETRIES is > 0 ? env.PDS_PROXY_MAX_RETRIES.Value : 3,
        PreferCompressed = env.PDS_PROXY_PREFER_COMPRESSED ?? false
    };
    
    public DiskBlobstoreConfig MapDiskBlobstoreConfig(ServerEnvironment env) => new DiskBlobstoreConfig
    {
        Location = env.PDS_BLOBSTORE_DISK_LOCATION,
        TempLocation = env.PDS_BLOBSTORE_DISK_TMP_LOCATION
    };
    
    public ServerConfig(ServerEnvironment env)
    {
        // run model validation on env
        var validationContext = new ValidationContext(env);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(env, validationContext, validationResults, true))
        {
            throw new Exception($"Invalid environment configuration: {string.Join(", ", validationResults.Select(x => x.ErrorMessage))}");
        }

        if (env.PDS_DATA_DIRECTORY != null && !Directory.Exists(env.PDS_DATA_DIRECTORY))
        {
            Directory.CreateDirectory(env.PDS_DATA_DIRECTORY);
        }
        
        if (!Directory.Exists(env.PDS_ACTOR_STORE_DIRECTORY))
        {
            Directory.CreateDirectory(env.PDS_ACTOR_STORE_DIRECTORY);
        }
        
        Service = MapServiceConfig(env);
        Subscription = MapSubscriptionConfig(env);
        Db = MapDatabaseConfig(env);
        ActorStore = MapActorStoreConfig(env);
        Blobstore = MapDiskBlobstoreConfig(env);
        Identity = MapIdentityConfig(env);
        Invites = MapInviteConfig(env);
        BskyAppView = MapBskyAppViewConfig(env);
        Proxy = MapProxyConfig(env);
        SecretsConfig = MapSecretsConfig(env);
        Crawlers = env.PDS_CRAWLERS.ToArray();
    }
    
    public static void RegisterServices(IServiceCollection services, ServerConfig config)
    {
        services.AddSingleton(config);
        services.AddSingleton(config.Service);
        services.AddSingleton(config.Db);
        services.AddSingleton(config.ActorStore);
        services.AddSingleton(config.Blobstore);
        services.AddSingleton(config.Identity);
        services.AddSingleton(config.Invites);
        services.AddSingleton(config.BskyAppView);
        services.AddSingleton(config.Proxy);
        services.AddSingleton(config.SecretsConfig);
        services.AddSingleton(config.Subscription);

        // AccountManager deps
        services.AddScoped<AccountRepository>();
        services.AddDbContext<AccountManagerDb>(x => x.UseSqlite($"Data Source={config.Db.AccountDbLoc}"));
        services.AddScoped<AccountStore>();
        services.AddScoped<PasswordStore>();
        services.AddScoped<RepoStore>();
        services.AddScoped<InviteStore>();
        services.AddScoped<EmailTokenStore>();
        services.AddScoped<Auth>();
        
        // Actor store
        services.AddScoped<ActorRepositoryProvider>();
        
        // Resolvers
        services.AddSingleton<IDidCache>(new MemoryCache(
            TimeSpan.FromMilliseconds(config.Identity.CacheStaleTTL),
            TimeSpan.FromMilliseconds(config.Identity.CacheMaxTTL)));
        services.AddSingleton<IdentityResolverOpts>(x => new IdentityResolverOpts
        {
            DidCache = x.GetRequiredService<IDidCache>(),
            PlcUrl = config.Identity.PlcUrl,
            TimeoutMs = config.Identity.ResolverTimeout
        });
        services.AddSingleton<IdResolver>();
        services.AddSingleton<HandleManager>();
        
        // AuthVerifier
        services.AddScoped<AuthVerifier>();
        services.AddSingleton<AuthVerifierConfig>(x => new AuthVerifierConfig(config.SecretsConfig.JwtSecret, "secret", config.Service.PublicUrl, config.Service.Did));
        
        // Sequencer
        services.AddDbContextFactory<SequencerDb>(x => x.UseSqlite($"Data Source={config.Db.SequencerDbLoc}"));
        services.AddScoped<SequencerRepository>();
        services.AddSingleton<Crawlers>();
        services.AddSingleton(x => new CrawlersConfig(config.Service.Hostname, config.Crawlers));
        
        // Plc
        services.AddSingleton(new PlcClientConfig(config.Identity.PlcUrl));
        services.AddSingleton<PlcClient>();
        
        // Mailer
        services.AddSingleton<IMailer, StubMailer>();
    }
}