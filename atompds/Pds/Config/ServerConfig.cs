namespace atompds.Pds.Config;

public record ServerConfig
{
    public ServiceConfig Service { get; init; }
    public DatabaseConfig Db { get; init; }
    public ActorStoreConfig ActorStore { get; init; }
    public DiskBlobstoreConfig Blobstore { get; init; }
    public IdentityConfig Identity { get; init; }
    public InvitesConfig Invites { get; init; }
    public IBskyAppViewConfig BskyAppView { get; init; }
    public ProxyConfig Proxy { get; init; }
    public SecretsConfig SecretsConfig { get; init; }

    public ServerConfig(ServerEnvironment env)
    {
        Service = env.ServiceConfig;
        Db = env.DatabaseConfig;
        ActorStore = env.ActorStoreConfig;
        Blobstore = env.DiskBlobstoreConfig;
        Identity = env.IdentityConfig;
        Invites = env.InviteConfig;
        BskyAppView = env.BskyAppViewConfig;
        Proxy = env.ProxyConfig;
        SecretsConfig = env.SecretsConfig;
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
    }
}