using atompds.Model;
using Microsoft.EntityFrameworkCore;

namespace atompds.Database;

public class ConfigRepository
{
    private readonly DataContext _db;
    private readonly ILogger<ConfigRepository> _logger;

    public ConfigRepository(DataContext db, ILogger<ConfigRepository> logger)
    {
        _db = db;
        _logger = logger;
    }
    
    public Task<ConfigRecord> GetConfigAsync()
    {
        return _db.Config.SingleAsync();
    }
}