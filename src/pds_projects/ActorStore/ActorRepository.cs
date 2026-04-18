using ActorStore.Db;
using ActorStore.Repo;
using BlobStore;
using Crypto;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Repo;
using System.Text.Json;

namespace ActorStore;

public class ActorRepository : IDisposable, IAsyncDisposable
{
    private readonly ActorStoreDb _db;
    private readonly SqlRepoTransactor _sqlRepoTransactor;

    public ActorRepository(ActorStoreDb db, string did, IKeyPair keyPair, IBlobStore blobStore)
    {
        _db = db;
        _sqlRepoTransactor = new SqlRepoTransactor(db, did);
        Record = new RecordRepository(db, did, keyPair, _sqlRepoTransactor);
        Repo = new RepoRepository(db, did, keyPair, _sqlRepoTransactor, Record, blobStore);
    }
    public SqliteConnection? Connection => _db.Database.GetDbConnection() as SqliteConnection;
    public RepoRepository Repo { get; }
    public RecordRepository Record { get; }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    public string[] ListCollections(string did, ActorStoreDb db)
    {
        return db.Records
            .Where(r => r.RepoRev == did)
            .Select(r => r.Collection)
            .Distinct()
            .ToArray();
    }

    public async Task<T> TransactDbAsync<T>(Func<ActorStoreDb, Task<T>> fn)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var result = await fn(_db);
            await tx.CommitAsync();
            return result;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<T> TransactRepoAsync<T>(Func<ActorRepository, Task<T>> fn)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var result = await fn(this);
            await tx.CommitAsync();
            return result;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<JsonElement>> GetPreferencesAsync(string scope)
    {
        var scopePrefix = $"{scope}:";
        var rows = await _db.AccountPrefs
            .Where(x => x.Name.StartsWith(scopePrefix))
            .OrderBy(x => x.Name)
            .Select(x => x.ValueJson)
            .ToListAsync();

        var preferences = new List<JsonElement>(rows.Count);
        foreach (var row in rows)
        {
            using var document = JsonDocument.Parse(row);
            preferences.Add(document.RootElement.Clone());
        }

        return preferences;
    }

    public async Task PutPreferencesAsync(string scope, IReadOnlyList<JsonElement> preferences)
    {
        var scopePrefix = $"{scope}:";
        await _db.AccountPrefs
            .Where(x => x.Name.StartsWith(scopePrefix))
            .ExecuteDeleteAsync();

        for (var i = 0; i < preferences.Count; i++)
        {
            _db.AccountPrefs.Add(new AccountPref
            {
                Name = $"{scopePrefix}{i:D4}",
                ValueJson = preferences[i].GetRawText()
            });
        }

        await _db.SaveChangesAsync();
    }
}
