using System.Text;
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
        ValidationStatus validationStatus = ValidationStatus.Valid;
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
            [], // TODO: BlobRefs need to be parsed out of the record
            validationStatus);
    }
    
    

    public static PreparedUpdate PrepareUpdate(string did, string collection, string rkey, Cid? swapCid, ATObject record, bool? validate)
    {
        var maybeValidate = validate != false;
        if (record.Type != collection && maybeValidate)
        {
            throw new Exception($"Invalid type, expected {collection}, got {record.Type}");
        }
        
        ValidationStatus validationStatus = ValidationStatus.Valid;
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
            [],
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
                    if (facet.Features == null) continue;
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
}