using System.Text;
using System.Text.Json;
using CID;
using Common;
using FishyFlip.Lexicon;
using FishyFlip.Lexicon.App.Bsky.Actor;
using FishyFlip.Lexicon.App.Bsky.Feed;
using FishyFlip.Lexicon.App.Bsky.Graph;
using FishyFlip.Lexicon.App.Bsky.Richtext;
using FishyFlip.Models;
using Handle;
using PeterO.Cbor;
using Repo;

namespace ActorStore.Repo;

public class Prepare
{
    public static PreparedDelete PrepareDelete(string did, string collection, string rkey, Cid? swapCid)
    {
        return new PreparedDelete(ATUri.Create($"{did}/{collection}/{rkey}"), swapCid);
    }

    public static PreparedCreate PrepareCreate(string did, string collection, string? rkey, Cid? swap, ATObject record, bool? validate)
    {
        var maybeValidate = validate != false;
        if (record.Type != collection && maybeValidate)
        {
            throw new Exception($"Invalid type, expected {collection}, got {record.Type}");
        }

        // TODO: need to properly validate the record
        var validationStatus = ValidationStatus.Unknown;
        if (maybeValidate)
        {
            // TODO:
            // 1. ensure type exists
            // 2. validate against lexicon
            // 3. ensure createdAt is valid
        }

        var nextRKey = TID.Next();
        rkey ??= nextRKey.ToString();
        RecordKey.EnsureValidRecordKey(rkey);
        AssertNoExplicitSlurs(rkey, record);
        var recordCbor = CBORObject.FromJSONString(record.ToJson());
        return new PreparedCreate(
            ATUri.Create($"{did}/{collection}/{rkey}"),
            CidForSafeRecord(record),
            swap,
            recordCbor,
            ExtractBlobReferences(record.ToJson()),
            validationStatus);
    }


    public static PreparedUpdate PrepareUpdate(string did, string collection, string rkey, Cid? swapCid, ATObject record, bool? validate)
    {
        var maybeValidate = validate != false;
        if (record.Type != collection && maybeValidate)
        {
            throw new Exception($"Invalid type, expected {collection}, got {record.Type}");
        }

        var validationStatus = ValidationStatus.Unknown;
        if (maybeValidate)
        {
            //
        }

        AssertNoExplicitSlurs(rkey, record);
        return new PreparedUpdate(
            ATUri.Create($"{did}/{collection}/{rkey}"),
            CidForSafeRecord(record),
            swapCid,
            CBORObject.FromJSONString(record.ToJson()),
            ExtractBlobReferences(record.ToJson()),
            validationStatus);
    }

    public static Cid CidForSafeRecord(ATObject record)
    {
        // TODO: This is probably not in any way correct.
        var cborObj = CBORObject.FromJSONString(record.ToJson());
        var block = CborBlock.Encode(cborObj);

        return block.Cid;
    }

    public static void AssertNoExplicitSlurs(string rkey, ATObject record)
    {
        var sb = new StringBuilder();
        if (record is Profile profile)
        {
            sb.Append(' ');
            sb.Append(profile.DisplayName);
        }
        else if (record is List list)
        {
            sb.Append(' ');
            sb.Append(list.Name);
        }
        else if (record is Starterpack starterpack)
        {
            sb.Append(' ');
            sb.Append(starterpack.Name);
        }
        else if (record is Generator generator)
        {
            sb.Append(' ');
            sb.Append(rkey);
            sb.Append(' ');
            sb.Append(generator.DisplayName);
        }
        else if (record is Post post)
        {
            if (post.Tags != null)
            {
                sb.Append(' ');
                sb.AppendJoin(" ", post.Tags);
            }

            if (post.Facets != null)
            {
                foreach (var facet in post.Facets)
                {
                    if (facet.Features == null)
                    {
                        continue;
                    }
                    foreach (var feature in facet.Features)
                    {
                        if (feature is Tag tag)
                        {
                            sb.Append(' ');
                            sb.Append(tag.TagValue);
                        }
                    }
                }
            }
        }

        if (HandleManager.HasExplicitSlur(sb.ToString()))
        {
            throw new Exception("Unacceptable slur in record.");
        }
    }


    static PreparedBlobRef[] ExtractBlobReferences(string json)
    {
        using var jsonDoc = JsonDocument.Parse(json);
        return ExtractBlobReferences(jsonDoc.RootElement);
    }

    static PreparedBlobRef[] ExtractBlobReferences(JsonElement root)
    {
        // Do a BFS over the json graph
        // TODO: there is a constraits stuff they extract in the reference implementaion
        // I still don't know the purpose of it so I will leave it for now

        int MAX_LEVEL = 32;

        var IsRelevantType = (JsonElement elem) => 
            elem.ValueKind == JsonValueKind.Object || elem.ValueKind == JsonValueKind.Array;

        if (!IsRelevantType(root))
            return [];

        List<PreparedBlobRef> result = [];
        // parent might be useful later for constraints
        var q = new Queue<(JsonElement elem, int level, JsonElement? parent)>();

        q.Enqueue((root, 0, null));

        while (q.Any())
        {
            var cur = q.Dequeue();

            if (cur.level > MAX_LEVEL)
                continue;

            if (cur.elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in cur.elem.EnumerateArray().Where(IsRelevantType))
                    q.Enqueue((elem, cur.level + 1, cur.elem));
                
                continue;
            }

            var blobRef = TryExtractBlobReference(cur.elem);
            if (blobRef is not null)
            {
                result.Add(blobRef);
                continue;
            }

            foreach (var prop in cur.elem.EnumerateObject().Where(p => IsRelevantType(p.Value)))
            {
                q.Enqueue((prop.Value, cur.level + 1, cur.elem));
            }

        }

        return result.ToArray();
    }

    static PreparedBlobRef? TryExtractBlobReference(JsonElement elem)
    {
        // https://atproto.com/specs/data-model#blob-type
        // TODO: maybe support legacy blob format
        if (elem.ValueKind != JsonValueKind.Object)
            return null;

        try
        {
            var typeElem = elem.GetProperty("$type");
            if (typeElem.ValueKind != JsonValueKind.String)
                return null;
            
            string type = typeElem.GetString()!;

            if (type != "blob")
                return null;

            var mimeTypeElem = elem.GetProperty("mimeType");
            if (mimeTypeElem.ValueKind != JsonValueKind.String)
                return null;

            if (string.IsNullOrWhiteSpace(mimeTypeElem.GetString()))
                return null;

            string mimeType = mimeTypeElem.GetString()!;

            var sizeElem = elem.GetProperty("size");
            if (sizeElem.ValueKind != JsonValueKind.Number)
                return null;

            long size = sizeElem.GetInt64();
            if (size <= 0)
                return null;


            var refObj = elem.GetProperty("ref");
            if (refObj.ValueKind != JsonValueKind.Object)
                return null;

            var cidElem = refObj.GetProperty("$link");
            if (cidElem.ValueKind != JsonValueKind.String)
                return null;

            string cidStr = cidElem.GetString()!;

            if (string.IsNullOrWhiteSpace(cidStr))
                return null;

            var cid = Cid.FromString(cidStr);

            // TODO: constraints
            return new PreparedBlobRef(cid, mimeType, size, new(null, null));

        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (CIDException)
        {
            return null;
        }
    }

}