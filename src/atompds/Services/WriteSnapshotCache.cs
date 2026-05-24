using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace atompds.Services;

public class WriteSnapshotCache
{
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, List<WriteSnapshot>> _snapshots = new();
    private readonly ILogger<WriteSnapshotCache> _logger;
    private DateTime _lastCleanup = DateTime.UtcNow;

    public WriteSnapshotCache(ILogger<WriteSnapshotCache> logger)
    {
        _logger = logger;
    }

    public void AddWrite(string did, string collection, string rkey, string recordJson, string cid, string rev)
    {
        CleanupIfNeeded();

        var snapshot = new WriteSnapshot
        {
            Did = did,
            Collection = collection,
            Rkey = rkey,
            RecordJson = recordJson,
            Cid = cid,
            Rev = rev,
            Timestamp = DateTime.UtcNow
        };

        var key = $"{did}:{collection}";
        _snapshots.AddOrUpdate(key,
            [snapshot],
            (_, existing) =>
            {
                existing.Add(snapshot);
                return existing;
            });
    }

    public void RemoveWrite(string did, string collection, string rkey)
    {
        var key = $"{did}:{collection}";
        if (_snapshots.TryGetValue(key, out var entries))
        {
            entries.RemoveAll(s => s.Rkey == rkey);
            if (entries.Count == 0)
            {
                _snapshots.TryRemove(key, out _);
            }
        }
    }

    public List<JsonElement> GetSnapshotsForCollection(string did, string collection)
    {
        CleanupIfNeeded();

        var key = $"{did}:{collection}";
        if (!_snapshots.TryGetValue(key, out var entries))
            return [];

        var now = DateTime.UtcNow;
        return entries
            .Where(s => now - s.Timestamp < _ttl)
            .Select(s =>
            {
                using var doc = JsonDocument.Parse(s.RecordJson);
                var wrapped = new
                {
                    uri = $"at://{did}/{collection}/{s.Rkey}",
                    cid = s.Cid,
                    value = doc.RootElement
                };
                var json = JsonSerializer.Serialize(wrapped);
                using var resultDoc = JsonDocument.Parse(json);
                return resultDoc.RootElement.Clone();
            })
            .ToList();
    }

    public WriteSnapshot? GetSnapshot(string did, string collection, string rkey)
    {
        var key = $"{did}:{collection}";
        if (!_snapshots.TryGetValue(key, out var entries))
            return null;

        var now = DateTime.UtcNow;
        return entries.FirstOrDefault(s => s.Rkey == rkey && now - s.Timestamp < _ttl);
    }

    public void InvalidateForDid(string did)
    {
        var keysToRemove = _snapshots.Keys.Where(k => k.StartsWith($"{did}:")).ToList();
        foreach (var key in keysToRemove)
        {
            _snapshots.TryRemove(key, out _);
        }
    }

    private void CleanupIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanup < TimeSpan.FromMinutes(1)) return;

        _lastCleanup = now;
        foreach (var kvp in _snapshots)
        {
            kvp.Value.RemoveAll(s => now - s.Timestamp > _ttl);
            if (kvp.Value.Count == 0)
            {
                _snapshots.TryRemove(kvp.Key, out _);
            }
        }
    }
}

public class WriteSnapshot
{
    public string Did { get; set; } = "";
    public string Collection { get; set; } = "";
    public string Rkey { get; set; } = "";
    public string RecordJson { get; set; } = "";
    public string Cid { get; set; } = "";
    public string Rev { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
