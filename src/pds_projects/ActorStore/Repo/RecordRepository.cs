using System.Text.Json;
using ActorStore.Db;
using CarpaNet;
using CID;
using Crypto;
using Microsoft.EntityFrameworkCore;
using PeterO.Cbor;
using Repo;
using Util = CommonWeb.Util;

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
    public async Task<GetRecordResult?> GetRecordAsync(ATUri uri, string? cid, bool includeSoftDeleted = false)
    {
        var uriStr = uri.ToString();
        var query = _db.Records
            .Where(x => x.Uri == uriStr);
        if (!includeSoftDeleted)
        {
            query = query.Where(x => x.TakedownRef == null);
        }
        if (cid != null)
        {
            query = query.Where(x => x.Cid == cid);
        }

        // TODO: Innerjoin repoBlock table

        var record = await query.FirstOrDefaultAsync();
        if (record == null)
        {
            return null;
        }
        var block = await _db.RepoBlocks.FirstOrDefaultAsync(x => x.Cid == record.Cid);
        if (block == null)
        {
            return null;
        }
        return new GetRecordResult(record.Uri, record.Cid, ToJsonElement(CBORObject.DecodeFromBytes(block.Content)), record.IndexedAt, record.TakedownRef);
    }

    public async Task IndexRecordAsync(ATUri uri, Cid cid, CBORObject? record, WriteOpAction action, string rev, DateTime? timestamp)
    {
        var row = new Record
        {
            Uri = uri.ToString(),
            Cid = cid.ToString(),
            Collection = uri.Collection ?? throw new Exception("Invalid collection"),
            Rkey = uri.RecordKey ?? throw new Exception("Invalid rkey"),
            RepoRev = rev,
            IndexedAt = timestamp ?? DateTime.UtcNow
        };
        if (!(uri.Authority?.StartsWith("did:") ?? false))
        {
            throw new Exception("Invalid hostname");
        }
        if (row.Collection.Length < 1)
        {
            throw new Exception("Invalid collection");
        }
        if (row.Rkey.Length < 1)
        {
            throw new Exception("Invalid rkey");
        }

        Record? existing = null;
        if (action == WriteOpAction.Update)
        {
            existing = await _db.Records.AsNoTracking().FirstOrDefaultAsync(x => x.Uri == uri.ToString());
        }

        if (existing != null)
        {
            _db.Records.Update(row);
        }
        else
        {
            _db.Records.Add(row);
        }

        if (record != null)
        {
            var backlinks = GetBacklinks(uri, record);
            if (action == WriteOpAction.Update)
            {
                await RemoveBacklinksByUriAsync(uri.ToString());
            }
            await AddBacklinksAsync(backlinks);
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteRecordAsync(ATUri uri)
    {
        await _db.Records.Where(x => x.Uri == uri.ToString()).ExecuteDeleteAsync();
        await _db.Backlinks.Where(x => x.Uri == uri.ToString()).ExecuteDeleteAsync();
    }

    public async Task RemoveBacklinksByUriAsync(string uri)
    {
        await _db.Backlinks.Where(x => x.Uri == uri).ExecuteDeleteAsync();
    }

    public async Task AddBacklinksAsync(Backlink[] backlinks)
    {
        if (backlinks.Length == 0)
        {
            return;
        }
        foreach (var backlink in backlinks)
        {
            var conflict = await _db.Backlinks.FirstOrDefaultAsync(x => x.Uri == backlink.Uri && x.Path == backlink.Path);
            if (conflict != null)
            {
                continue;
            }
            _db.Backlinks.Add(backlink);
        }

        await _db.SaveChangesAsync();
    }

    // Not really a fan of including lexicon specific parsing here
    public Backlink[] GetBacklinks(ATUri uri, CBORObject? record)
    {
        if (record == null)
        {
            return [];
        }
        var recordType = record.ContainsKey("$type") && !record["$type"].IsNull ? record["$type"].AsString() : null;
        if (recordType == "app.bsky.graph.follow" || recordType == "app.bsky.graph.block")
        {
            var subject = record["subject"].Type == CBORType.TextString ? record["subject"].AsString() : null;
            if (subject == null)
            {
                return [];
            }

            try
            {
                Util.EnsureValidDid(subject);
            }
            catch (Exception)
            {
                return [];
            }

            return
            [
                new Backlink
                {
                    Uri = uri.ToString(),
                    Path = "subject",
                    LinkTo = subject
                }
            ];
        }

        if (recordType == "app.bsky.feed.like" || recordType == "app.bsky.feed.repost")
        {
            var subject = record["subject"];
            if (subject != null && subject["uri"].Type != CBORType.TextString)
            {
                return [];
            }

            var subjectUri = subject["uri"].AsString();
            try
            {
                Util.EnsureValidAtUri(subjectUri);
            }
            catch (Exception)
            {
                return [];
            }

            return
            [
                new Backlink
                {
                    Uri = uri.ToString(),
                    Path = "subject.uri",
                    LinkTo = subjectUri
                }
            ];
        }

        return [];
    }

    public async Task<List<RecordForCollection>> ListRecordsForCollectionAsync(
        string collection,
        int limit,
        bool reverse,
        string? cursor,
        bool includeSoftDeleted = false
    )
    {
        var qb = _db.Records
            .Join(_db.RepoBlocks,
                record => record.Cid,
                repoBlock => repoBlock.Cid,
                (record, repoBlock) => new { Record = record, RepoBlock = repoBlock });

        qb = qb.Where(x => x.Record.Collection == collection);

        if (!includeSoftDeleted)
            qb = qb.Where(x => x.Record.TakedownRef == null);

        qb = reverse
            ? qb.OrderByDescending(x => x.Record.Rkey)
            : qb.OrderBy(x => x.Record.Rkey);


        if (!string.IsNullOrEmpty(cursor))
        {
            if (reverse)
            {
                qb = qb.Where(x => string.Compare(x.Record.Rkey, cursor!) < 0);
            }
            else
            {
                qb = qb.Where(x => string.Compare(x.Record.Rkey, cursor!) > 0);
            }
        }

        qb = qb.Take(limit);


        var results = await qb.ToListAsync();

        return results.Select(x => new RecordForCollection(
            x.Record.Uri,
            x.Record.Cid,
            ToJsonElement(CBORObject.DecodeFromBytes(x.RepoBlock.Content))
        )).ToList();
    }


    public record GetRecordResult(string Uri, string Cid, JsonElement Value, DateTime IndexedAt, string? TakedownRef);

    public record RecordForCollection(
        string Uri,
        string Cid,
        JsonElement Value
    );

    private static JsonElement ToJsonElement(CBORObject value)
    {
        using var document = JsonDocument.Parse(value.ToJSONString());
        return document.RootElement.Clone();
    }
}
