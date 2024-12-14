using ActorStore.Db;
using ActorStore.Repo;
using Config;
using Crypto;
using Crypto.Secp256k1;
using FishyFlip.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xrpc;

namespace ActorStore;

public class ActorRepository : IDisposable, IAsyncDisposable
{
    private readonly ActorStoreDb _db;
    public SqliteConnection? Connection => _db.Database.GetDbConnection() as SqliteConnection;
    private readonly SqlRepoTransactor _sqlRepoTransactor;
    public RepoRepository Repo { get; }
    public RecordRepository Record { get; }
    
    public ActorRepository(ActorStoreDb db, string did, IKeyPair keyPair)
    {
        _db = db;
        _sqlRepoTransactor = new SqlRepoTransactor(db, did);
        Record = new RecordRepository(db, did, keyPair, _sqlRepoTransactor);
        Repo = new RepoRepository(db, did, keyPair, _sqlRepoTransactor, Record);
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
    
    public void Dispose()
    {
        _db.Dispose();
    }
    
    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
    }
}