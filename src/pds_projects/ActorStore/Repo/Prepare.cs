using System.Globalization;
using System.Text;
using System.Text.Json;
using AppBsky.Actor;
using AppBsky.Feed;
using CarpaNet;
using CID;
using Common;
using Handle;
using Multiformats.Codec;
using Multiformats.Hash;
using PeterO.Cbor;
using Repo;
using Xrpc;

namespace ActorStore.Repo;

public class Prepare
{
    private static readonly HashSet<string> KnownCollections =
    [
        "app.bsky.feed.post",
        "app.bsky.actor.profile",
        "app.bsky.graph.follow",
        "app.bsky.feed.like",
        "app.bsky.feed.repost"
    ];

    public static PreparedDelete PrepareDelete(string did, string collection, string rkey, Cid? swapCid)
    {
        return new PreparedDelete(ATUri.Create(did, collection, rkey), swapCid);
    }

    public static PreparedCreate PrepareCreate(string did, string collection, string? rkey, Cid? swapCid, JsonElement record, bool? validate, TimeSpan? createdAtTolerance = null)
    {
        var maybeValidate = validate != false;
        var recordType = GetRecordType(record);
        if (recordType != collection && maybeValidate)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Invalid type, expected {collection}, got {recordType}"));
        }

        var validationStatus = ValidateRecord(record, collection, maybeValidate, createdAtTolerance);

        var nextRKey = TID.Next();
        rkey ??= nextRKey.ToString();
        RecordKey.EnsureValidRecordKey(rkey);
        AssertNoExplicitSlurs(collection, rkey, record);
        var recordJson = record.GetRawText();
        var recordCbor = CBORObject.FromJSONString(recordJson);
        return new PreparedCreate(
            ATUri.Create(did, collection, rkey),
            CidForSafeRecord(record),
            swapCid,
            recordCbor,
            ExtractBlobReferences(record),
            validationStatus);
    }


    public static PreparedUpdate PrepareUpdate(string did, string collection, string rkey, Cid? swapCid, JsonElement record, bool? validate, TimeSpan? createdAtTolerance = null)
    {
        var maybeValidate = validate != false;
        var recordType = GetRecordType(record);
        if (recordType != collection && maybeValidate)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Invalid type, expected {collection}, got {recordType}"));
        }

        var validationStatus = ValidateRecord(record, collection, maybeValidate, createdAtTolerance);

        AssertNoExplicitSlurs(collection, rkey, record);
        return new PreparedUpdate(
            ATUri.Create(did, collection, rkey),
            CidForSafeRecord(record),
            swapCid,
            CBORObject.FromJSONString(record.GetRawText()),
            ExtractBlobReferences(record),
            validationStatus);
    }

    private static ValidationStatus ValidateRecord(JsonElement record, string collection, bool maybeValidate, TimeSpan? createdAtTolerance)
    {
        if (!maybeValidate)
        {
            return ValidationStatus.Unknown;
        }

        var recordType = GetRecordType(record);
        if (string.IsNullOrEmpty(recordType))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Missing $type in record"));
        }

        // createdAt future check (applies to all records)
        if (record.TryGetProperty("createdAt", out var createdAtProp) &&
            createdAtProp.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(createdAtProp.GetString(), null, DateTimeStyles.AdjustToUniversal, out var createdAt))
            {
                var tolerance = createdAtTolerance ?? TimeSpan.FromMinutes(1);
                if (createdAt > DateTime.UtcNow + tolerance)
                {
                    throw new XRPCError(new InvalidRequestErrorDetail("createdAt is in the future"));
                }
            }
        }

        // Known collection validation
        if (!KnownCollections.Contains(recordType))
        {
            return ValidationStatus.Unknown;
        }

        // Minimal field validation for the 5 most-written lexicons
        switch (recordType)
        {
            case "app.bsky.feed.post":
                EnsureRequiredField(record, "text");
                EnsureRequiredField(record, "createdAt");
                break;
            case "app.bsky.actor.profile":
                // profile has no strict required fields
                break;
            case "app.bsky.graph.follow":
                EnsureRequiredField(record, "subject");
                EnsureRequiredField(record, "createdAt");
                break;
            case "app.bsky.feed.like":
                EnsureRequiredField(record, "subject");
                EnsureRequiredField(record, "createdAt");
                break;
            case "app.bsky.feed.repost":
                EnsureRequiredField(record, "subject");
                EnsureRequiredField(record, "createdAt");
                break;
        }

        return ValidationStatus.Valid;
    }

    private static void EnsureRequiredField(JsonElement record, string fieldName)
    {
        if (!record.TryGetProperty(fieldName, out _) ||
            (record.TryGetProperty(fieldName, out var prop) &&
             (prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)))
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Missing required field: {fieldName}"));
        }
    }

    public static Cid CidForSafeRecord(JsonElement record)
    {
        var bytes = CanonicalDagCbor.Encode(record);
        var hash = Multihash.Encode(System.Security.Cryptography.SHA256.HashData(bytes), HashType.SHA2_256);
        return Cid.NewV1((ulong)MulticodecCode.MerkleDAGCBOR, hash);
    }

    public static void AssertNoExplicitSlurs(string collection, string rkey, JsonElement record)
    {
        var sb = new StringBuilder();
        if (collection == Profile.RecordType)
        {
            sb.Append(' ');
            AppendString(record, sb, "displayName");
        }
        else if (collection == AppBsky.Graph.List.RecordType)
        {
            sb.Append(' ');
            AppendString(record, sb, "name");
        }
        else if (collection == "app.bsky.graph.starterpack")
        {
            sb.Append(' ');
            AppendString(record, sb, "name");
        }
        else if (collection == Generator.RecordType)
        {
            sb.Append(' ');
            sb.Append(rkey);
            sb.Append(' ');
            AppendString(record, sb, "displayName");
        }
        else if (collection == Post.RecordType)
        {
            if (record.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            {
                sb.Append(' ');
                foreach (var tag in tags.EnumerateArray())
                {
                    if (tag.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(tag.GetString());
                        sb.Append(' ');
                    }
                }
            }

            if (record.TryGetProperty("facets", out var facets) && facets.ValueKind == JsonValueKind.Array)
            {
                foreach (var facet in facets.EnumerateArray())
                {
                    if (!facet.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }
                    foreach (var feature in features.EnumerateArray())
                    {
                        if (feature.ValueKind == JsonValueKind.Object &&
                            feature.TryGetProperty("$type", out var typeProp) &&
                            typeProp.ValueKind == JsonValueKind.String &&
                            typeProp.GetString() == "app.bsky.richtext.facet#tag" &&
                            feature.TryGetProperty("tag", out var tagProp) &&
                            tagProp.ValueKind == JsonValueKind.String)
                        {
                            sb.Append(' ');
                            sb.Append(tagProp.GetString());
                        }
                    }
                }
            }
        }

        if (HandleManager.HasExplicitSlur(sb.ToString()))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Unacceptable slur in record."));
        }
    }

    private static string? GetRecordType(JsonElement record)
    {
        if (record.ValueKind == JsonValueKind.Object &&
            record.TryGetProperty("$type", out var typeProp) &&
            typeProp.ValueKind == JsonValueKind.String)
        {
            return typeProp.GetString();
        }

        return null;
    }

    private static void AppendString(JsonElement record, StringBuilder sb, string propertyName)
    {
        if (record.ValueKind == JsonValueKind.Object &&
            record.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            sb.Append(property.GetString());
        }
    }


    public static PreparedBlobRef[] ExtractBlobReferences(string json)
    {
        using var jsonDoc = JsonDocument.Parse(json);
        return ExtractBlobReferences(jsonDoc.RootElement);
    }

    private static readonly Dictionary<(string RecordType, string FieldName), BlobConstraint> KnownBlobConstraints = new()
    {
        [("app.bsky.actor.profile", "avatar")] = new(["image/png", "image/jpeg", "image/webp"], 1_000_000),
        [("app.bsky.actor.profile", "banner")] = new(["image/png", "image/jpeg", "image/webp"], 1_000_000),
    };

    public static PreparedBlobRef[] ExtractBlobReferences(JsonElement root)
    {
        int MAX_LEVEL = 32;

        var IsRelevantType = (JsonElement elem) =>
            elem.ValueKind == JsonValueKind.Object || elem.ValueKind == JsonValueKind.Array;

        if (!IsRelevantType(root))
            return [];

        List<PreparedBlobRef> result = [];
        var q = new Queue<(JsonElement elem, int level, JsonElement? parent, string? fieldName)>();

        q.Enqueue((root, 0, null, null));

        while (q.Any())
        {
            var cur = q.Dequeue();

            if (cur.level > MAX_LEVEL)
                break;

            if (cur.elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in cur.elem.EnumerateArray().Where(IsRelevantType))
                    q.Enqueue((elem, cur.level + 1, cur.elem, cur.fieldName));

                continue;
            }

            var recordType = (cur.parent?.ValueKind == JsonValueKind.Object
                && cur.parent.Value.TryGetProperty("$type", out var typeProp) == true
                && typeProp.ValueKind == JsonValueKind.String)
                ? typeProp.GetString()
                : null;
            var blobRef = TryExtractBlobReference(cur.elem, recordType, cur.fieldName);
            if (blobRef is not null)
            {
                result.Add(blobRef);
                continue;
            }

            foreach (var prop in cur.elem.EnumerateObject().Where(p => IsRelevantType(p.Value)))
            {
                q.Enqueue((prop.Value, cur.level + 1, cur.elem, prop.Name));
            }
        }

        return result.ToArray();
    }

    public static PreparedBlobRef? TryExtractBlobReference(JsonElement elem, string? recordType = null, string? fieldName = null)
    {
        // https://atproto.com/specs/data-model#blob-type
        if (elem.ValueKind != JsonValueKind.Object)
            return null;

        try
        {
            // Legacy: { "cid": "...", "mimeType": "..." } with no $type
            if (!elem.TryGetProperty("$type", out _) &&
                elem.TryGetProperty("cid", out var legacyCidElem) &&
                elem.TryGetProperty("mimeType", out var legacyMimeElem))
            {
                var legacyCidStr = legacyCidElem.GetString();
                var legacyMime = legacyMimeElem.GetString();
                if (!string.IsNullOrWhiteSpace(legacyCidStr) && !string.IsNullOrWhiteSpace(legacyMime))
                {
                    try
                    {
                        var legacyCid = Cid.FromString(legacyCidStr);
                        var constraint = (recordType != null && fieldName != null && KnownBlobConstraints.TryGetValue((recordType, fieldName), out var c)) ? c : new(null, null);
                        return new PreparedBlobRef(legacyCid, legacyMime, 0, constraint);
                    }
                    catch
                    {
                        // invalid CID, fall through
                    }
                }
            }

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

            long size = 0;
            try
            {
                size = sizeElem.GetInt64();
            }
            catch
            {
                return null;
            }

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

            try
            {
                var cid = Cid.FromString(cidStr);

                if (cid.Codec != (ulong)MulticodecCode.Raw)
                    return null;

                // T-11: populate constraints from known lexicon fields
                var constraint = (recordType != null && fieldName != null && KnownBlobConstraints.TryGetValue((recordType, fieldName), out var c)) ? c : new(null, null);
                return new PreparedBlobRef(cid, mimeType, size, constraint);
            }
            catch
            {
                return null;
            }
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

}
