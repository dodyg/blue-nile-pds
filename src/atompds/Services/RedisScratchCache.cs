using StackExchange.Redis;

namespace atompds.Services;

public sealed class RedisScratchCache : IScratchCache
{
    private readonly IDatabase _database;

    public RedisScratchCache(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<string?> GetAsync(string key)
    {
        var value = await _database.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task SetAsync(string key, string value, TimeSpan? ttl = null)
    {
        await _database.StringSetAsync(key, value, ttl, When.Always);
    }

    public async Task DeleteAsync(string key)
    {
        await _database.KeyDeleteAsync(key);
    }
}
