using atompds.Model;
using Microsoft.Extensions.Options;

namespace atompds.Database;

public class DataContextSeeder
{
    private readonly DataContext _db;
    private readonly ILogger<ConfigRepository> _logger;
    private readonly IOptions<CliConfig> _cfg;

    public DataContextSeeder(DataContext db, ILogger<ConfigRepository> logger, IOptions<CliConfig> cfg)
    {
        _db = db;
        _logger = logger;
        _cfg = cfg;
    }
    
    public async Task SeedAsync()
    {
        await InitConfig();
    }
    
    private async Task InitConfig()
    {
        var cfgValue = _cfg.Value;
        if (!_db.Config.Any())
        {
            _logger.LogInformation("No config found, initializing with provided values {PdsPfx}, {PdsDid}", cfgValue.PdsPfx, cfgValue.PdsDid);
            var random = Random.Shared;
            var jwtBuffer = new byte[128];
            random.NextBytes(jwtBuffer);
            var jwtSecret = Convert.ToBase64String(jwtBuffer)[..32];
            _db.Config.Add(new ConfigRecord(1, cfgValue.PdsPfx, cfgValue.PdsDid, cfgValue.AvailableUserDomains, cfgValue.AppViewUrl, cfgValue.AppViewDid, jwtSecret));
            await _db.SaveChangesAsync();
        }
        else
        {
            _logger.LogInformation("Config found, skipping initialization");
        }
    }
}