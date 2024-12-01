using atompds.Pds.Config;
using Crypto;
using Crypto.Secp256k1;
using Microsoft.EntityFrameworkCore;
using Xrpc;

namespace atompds.Pds.ActorStore.Db;

public class ActorStore
{
    private readonly ActorStoreConfig _config;
    
    public ActorStore(ActorStoreConfig config)
    {
        _config = config;
    }

    public (string Directory, string DbLocation, string KeyLocation) GetLocation(string did)
    {
        var didHash = Crypto.Utils.Sha256Hex(did);
        // note: Sha256Hex doesn't return 0x prefix so no need to substring
        var directory = Path.Join(_config.Directory, didHash, did);
        var dbLocation = Path.Join(directory, "store.sqlite");
        var keyLocation = Path.Join(directory, "key");
        return (directory, dbLocation, keyLocation);
    }

    public bool Exists(string did)
    {
        var (directory, dbLocation, _) = GetLocation(did);
        return Directory.Exists(directory) && File.Exists(dbLocation);
    }
    
    public Types.IKeyPair KeyPair(string did)
    {
        var (_, _, keyLocation) = GetLocation(did);
        var privKey = File.ReadAllBytes(keyLocation);
        var kp = Secp256k1Keypair.Import(privKey);
        return kp;
    }
    
    public ActorStoreDb Open(string did)
    {
        var (_, dbLocation, _) = GetLocation(did);
        if (!File.Exists(dbLocation))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Repo not found"));
        }
        
        var connectionString = $"Data Source={dbLocation};";
        if (_config.DisableWalAutoCheckpoint)
        {
            connectionString += "wal_autocheckpoint=0;";
        }
        
        var options = new DbContextOptionsBuilder<ActorStoreDb>()
            .UseSqlite(connectionString)
            .Options;
        
        var actorStoreDb = new ActorStoreDb(options);
        try
        {
            var root = actorStoreDb.RepoRoots.FirstOrDefault();
        }
        catch (Exception ex)
        {
            actorStoreDb.Dispose();
            throw;
        }
        
        return actorStoreDb;
    }

    public void Create(string did, Types.IExportableKeyPair keyPair)
    {
        var location = GetLocation(did);
        Directory.CreateDirectory(location.Directory);
        if (File.Exists(location.DbLocation))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Repo already exists"));
        }
        
        var privKey = keyPair.Export();
        File.WriteAllBytes(location.KeyLocation, privKey);
        
        var connectionString = $"Data Source={location.DbLocation};";
        if (_config.DisableWalAutoCheckpoint)
        {
            connectionString += "wal_autocheckpoint=0;";
        }
        
        var options = new DbContextOptionsBuilder<ActorStoreDb>()
            .UseSqlite(connectionString)
            .Options;
        
        using var actorStoreDb = new ActorStoreDb(options);
        actorStoreDb.Database.EnsureCreated();
        // ensure WAL
        actorStoreDb.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        
        // TODO: Migration
    }
    
    public void Destroy(string did)
    {
        // TODO: delete blobstore
        var location = GetLocation(did);
        
        if (Directory.Exists(location.Directory))
        {
            Directory.Delete(location.Directory, true);
        }
    }
}