using System.Text.Json;
using CommonWeb;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Identity;

public class SqliteDIDCache : IDidCache, IDisposable
{
    private readonly string _connectionString;
    private readonly TimeSpan _staleTtl;
    private readonly TimeSpan _maxTtl;
    private readonly ILogger<SqliteDIDCache> _logger;

    public SqliteDIDCache(string dbPath, TimeSpan staleTtl, TimeSpan maxTtl, ILogger<SqliteDIDCache> logger)
    {
        _connectionString = $"Data Source={dbPath}";
        _staleTtl = staleTtl;
        _maxTtl = maxTtl;
        _logger = logger;
        EnsureTable();
    }

    private void EnsureTable()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS did_cache (
                did TEXT PRIMARY KEY,
                doc TEXT NOT NULL,
                updatedAt DATETIME NOT NULL,
                staleAt DATETIME NOT NULL,
                expiresAt DATETIME NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_did_cache_expires ON did_cache(expiresAt);
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task CacheDidAsync(string did, DidDocument doc, CacheResult? prevResult = null)
    {
        var now = DateTime.UtcNow;
        var docJson = JsonSerializer.Serialize(doc);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO did_cache (did, doc, updatedAt, staleAt, expiresAt)
            VALUES ($did, $doc, $updatedAt, $staleAt, $expiresAt)
            ON CONFLICT(did) DO UPDATE SET
                doc = excluded.doc,
                updatedAt = excluded.updatedAt,
                staleAt = excluded.staleAt,
                expiresAt = excluded.expiresAt;
            """;
        cmd.Parameters.AddWithValue("$did", did);
        cmd.Parameters.AddWithValue("$doc", docJson);
        cmd.Parameters.AddWithValue("$updatedAt", now.ToString("o"));
        cmd.Parameters.AddWithValue("$staleAt", now.Add(_staleTtl).ToString("o"));
        cmd.Parameters.AddWithValue("$expiresAt", now.Add(_maxTtl).ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<CacheResult?> CheckCacheAsync(string did)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT doc, updatedAt, staleAt, expiresAt FROM did_cache WHERE did = $did;";
        cmd.Parameters.AddWithValue("$did", did);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var docJson = reader.GetString(0);
        var updatedAt = reader.GetDateTime(1);
        var staleAt = reader.GetDateTime(2);
        var expiresAt = reader.GetDateTime(3);
        var now = DateTime.UtcNow;

        if (now > expiresAt)
        {
            return null;
        }

        var doc = JsonSerializer.Deserialize<DidDocument>(docJson);
        if (doc == null)
        {
            return null;
        }

        return new CacheResult
        {
            Did = did,
            Doc = doc,
            UpdatedAt = updatedAt,
            Stale = now > staleAt,
            Expired = false
        };
    }

    public async Task RefreshCacheAsync(string did, Func<Task<DidDocument?>> getDoc, CacheResult? prevResult = null)
    {
        try
        {
            var doc = await getDoc();
            if (doc != null)
            {
                await CacheDidAsync(did, doc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background DID cache refresh failed for {Did}", did);
        }
    }

    public async Task ClearEntryAsync(string did)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM did_cache WHERE did = $did;";
        cmd.Parameters.AddWithValue("$did", did);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM did_cache;";
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        // SqliteConnection is disposed per-operation
    }
}
