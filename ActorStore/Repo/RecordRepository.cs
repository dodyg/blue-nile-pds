using ActorStore.Db;
using Crypto;
using FishyFlip.Models;
using Microsoft.EntityFrameworkCore;
using PeterO.Cbor;
using Repo;

namespace ActorStore.Repo;

public class RecordRepository
{
    private readonly ActorStoreDb _db;
    private readonly string _did;
    private readonly IKeyPair _keyPair;
    private readonly SqlRepoTransactor _storage;
    public RecordRepository(ActorStoreDb db, string did, IKeyPair keyPair, SqlRepoTransactor storage)
    {
        _db = db;
        _did = did;
        _keyPair = keyPair;
        _storage = storage;
    }

    public async Task IndexRecord(ATUri uri, CID.Cid cid, CBORObject? record, WriteOpAction action, string rev, DateTime? timestamp)
    {
        var row = new Record
        {
            Uri = uri.ToString(),
            Cid = cid.ToString(),
            Collection = uri.Collection,
            Rkey = uri.Rkey,
            RepoRev = rev,
            IndexedAt = timestamp ?? DateTime.UtcNow,
        };
        if (!uri.Hostname.StartsWith("did:"))
        {
            throw new Exception("Invalid hostname");
        }
        else if (row.Collection.Length < 1)
        {
            throw new Exception("Invalid collection");
        }
        else if (row.Rkey.Length < 1)
        {
            throw new Exception("Invalid rkey");
        }

        _db.Records.Add(row);

        if (record != null)
        {
            // TODO: Maintain backlinks
            // getBacklinks 
            // if (update){ remove by uri }
            // then addBacklinks
        }
        
        await _db.SaveChangesAsync();
    }

    public async Task DeleteRecord(ATUri uri)
    {
        await _db.Records.Where(x => x.Uri == uri.ToString()).ExecuteDeleteAsync();
        await _db.Backlinks.Where(x => x.Uri == uri.ToString()).ExecuteDeleteAsync();
    }
    
    public async Task RemoveBacklinksByUri(string uri)
    {
        await _db.Backlinks.Where(x => x.Uri == uri).ExecuteDeleteAsync();
    }

    public async Task AddBacklinks(Backlink[] backlinks)
    {
        if (backlinks.Length == 0) return;
        _db.Backlinks.AddRange(backlinks);
        await _db.SaveChangesAsync();
    }
}