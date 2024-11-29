using atompds.Config;
using Microsoft.Extensions.Options;

namespace atompds.Database;

public class DataContextSeeder
{
    private readonly DataContext _db;
    private readonly ILogger<ConfigRepository> _logger;
    private readonly ServiceConfig _env;

    public DataContextSeeder(DataContext db, ILogger<ConfigRepository> logger, ServiceConfig env)
    {
        _db = db;
        _logger = logger;
        _env = env;
    }
    
    public async Task SeedAsync()
    {
        await InitConfig();
    }
    
    private async Task InitConfig()
    {
        if (!_db.Config.Any())
        {
            var random = Random.Shared;
            var jwtBuffer = new byte[128];
            random.NextBytes(jwtBuffer);
            var jwtSecret = Convert.ToBase64String(jwtBuffer)[..32];
            _db.Config.Add(new ConfigRecord(1, 
                _env.ServiceConfig.Hostname, 
                _env.ServiceConfig.Did, 
                _env.PDS_SERVICE_HANDLE_DOMAINS.ToArray(), 
                _env.BskyAppViewConfig?.Url, _env.BskyAppViewConfig?.Did, jwtSecret));
            await _db.SaveChangesAsync();
        }
        else
        {
            _logger.LogInformation("Config found, skipping initialization");
        }
    }
}