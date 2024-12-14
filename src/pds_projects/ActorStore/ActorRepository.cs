using ActorStore.Db;
using ActorStore.Repo;
using Crypto;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ActorStore;

public class ActorRepository : IDisposable, IAsyncDisposable
{
    private readonly ActorStoreDb _db;
    private readonly SqlRepoTransactor _sqlRepoTransactor;

    public ActorRepository(ActorStoreDb db, string did, IKeyPair keyPair)
    {
        _db = db;
        _sqlRepoTransactor = new SqlRepoTransactor(db, did);
        Record = new RecordRepository(db, did, keyPair, _sqlRepoTransactor);
        Repo = new RepoRepository(db, did, keyPair, _sqlRepoTransactor, Record);
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

    public async Task<T> TransactDb<T>(Func<ActorStoreDb, Task<T>> fn)
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

    public async Task<T> TransactRepo<T>(Func<ActorRepository, Task<T>> fn)
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
}