using System.Formats.Cbor;
using System.Text;
using System.Text.Json;
using BlueNilePds.Core.CID;
using PeterO.Cbor;

namespace BlueNilePds.Core.Common;

/// <summary>
/// Thrown when a record payload cannot be represented as canonical DAG-CBOR
/// (for example a malformed <c>$link</c> / <c>$bytes</c> special object).
/// </summary>
public class DagCborEncodingException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public static class CanonicalDagCbor
{
    private const string LinkKey = "$link";
    private const string BytesKey = "$bytes";

    private static bool IsSpecialKey(string name) => name is LinkKey or BytesKey;

    /// <summary>
    /// Encode a <see cref="JsonElement"/> as canonical DAG-CBOR bytes.
    /// Map keys are sorted by UTF-8 byte order.
    /// </summary>
    public static byte[] Encode(JsonElement element)
    {
        var writer = new CborWriter(CborConformanceMode.Canonical, false);
        WriteElement(writer, element);
        return writer.Encode();
    }

    /// <summary>
    /// Re-encode an already-decoded PeterO <see cref="CBORObject"/> as canonical DAG-CBOR bytes.
    /// Map keys are sorted by UTF-8 byte order. Useful for converting legacy
    /// (non-canonical) stored blocks to the canonical form.
    /// </summary>
    public static byte[] Encode(CBORObject obj)
    {
        var writer = new CborWriter(CborConformanceMode.Canonical, false);
        WriteValue(writer, obj);
        return writer.Encode();
    }

    private static void WriteValue(CborWriter writer, CBORObject obj)
    {
        if (obj.IsTagged)
        {
            foreach (var tag in obj.GetAllTags())
            {
                writer.WriteTag((CborTag)(long)tag);
            }
            obj = obj.Untag();
        }

        switch (obj.Type)
        {
            case CBORType.Map:
                if (TryNormalizeSpecialMap(obj, writer))
                {
                    return;
                }

                var keys = obj.Keys.ToList()
                    .OrderBy(k => k.AsString(), Utf8ByteComparer.Instance)
                    .ToList();
                writer.WriteStartMap(keys.Count);
                foreach (var key in keys)
                {
                    writer.WriteTextString(key.AsString());
                    WriteValue(writer, obj[key]);
                }
                writer.WriteEndMap();
                break;

            case CBORType.Array:
                writer.WriteStartArray(obj.Count);
                for (var i = 0; i < obj.Count; i++)
                {
                    WriteValue(writer, obj[i]);
                }
                writer.WriteEndArray();
                break;

            case CBORType.TextString:
                writer.WriteTextString(obj.AsString());
                break;

            case CBORType.ByteString:
                writer.WriteByteString(obj.GetByteString());
                break;

            case CBORType.Integer:
                writer.WriteInt64(obj.AsNumber().ToInt64Checked());
                break;

            case CBORType.FloatingPoint:
                writer.WriteDouble(obj.AsDouble());
                break;

            case CBORType.Boolean:
                writer.WriteBoolean(obj.AsBoolean());
                break;

            default:
                if (obj.IsNumber && obj.AsNumber().IsInteger())
                {
                    writer.WriteInt64(obj.AsNumber().ToInt64Checked());
                }
                else if (obj.IsNumber)
                {
                    writer.WriteDouble(obj.AsDouble());
                }
                else if (obj.IsNull || obj.IsUndefined)
                {
                    writer.WriteNull();
                }
                else
                {
                    throw new NotSupportedException($"CBORType.{obj.Type} is not supported in DAG-CBOR");
                }
                break;
        }
    }

    private static void WriteElement(CborWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (TryWriteSpecialObject(writer, element))
                {
                    break;
                }

                var props = element.EnumerateObject()
                    .OrderBy(p => p.Name, Utf8ByteComparer.Instance)
                    .ToList();
                writer.WriteStartMap(props.Count);
                foreach (var prop in props)
                {
                    writer.WriteTextString(prop.Name);
                    WriteElement(writer, prop.Value);
                }
                writer.WriteEndMap();
                break;

            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToList();
                writer.WriteStartArray(items.Count);
                foreach (var item in items)
                {
                    WriteElement(writer, item);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteTextString(element.GetString()!);
                break;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longVal))
                {
                    writer.WriteInt64(longVal);
                }
                else if (element.TryGetDouble(out var doubleVal))
                {
                    writer.WriteDouble(doubleVal);
                }
                else
                {
                    throw new NotSupportedException($"Number {element.GetRawText()} cannot be represented in DAG-CBOR");
                }
                break;

            case JsonValueKind.True:
                writer.WriteBoolean(true);
                break;

            case JsonValueKind.False:
                writer.WriteBoolean(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNull();
                break;

            default:
                throw new NotSupportedException($"JsonValueKind.{element.ValueKind} is not supported in DAG-CBOR");
        }
    }

    /// <summary>
    /// JSON special objects (<c>{"$link": "..."}</c> / <c>{"$bytes": "..."}</c>)
    /// are JSON-only representations. In DAG-CBOR they must be a tag-42 link or
    /// a byte string respectively, never a map.
    /// </summary>
    private static bool TryWriteSpecialObject(CborWriter writer, JsonElement element)
    {
        var props = element.EnumerateObject().ToList();
        if (!props.Any(p => IsSpecialKey(p.Name)))
        {
            return false;
        }

        if (props.Count != 1)
        {
            throw new DagCborEncodingException(
                $"Invalid DAG-CBOR: '{props[0].Name}' special object must not have sibling properties.");
        }

        var prop = props[0];
        if (prop.Value.ValueKind != JsonValueKind.String)
        {
            throw new DagCborEncodingException(
                $"Invalid DAG-CBOR: '{prop.Name}' must be a string.");
        }

        WriteSpecialValue(writer, prop.Name, prop.Value.GetString()!);
        return true;
    }

    /// <summary>
    /// Normalizes legacy blocks that stored the JSON special-object form as a
    /// CBOR map into the spec-required link/byte-string form.
    /// </summary>
    private static bool TryNormalizeSpecialMap(CBORObject obj, CborWriter writer)
    {
        if (obj.Count != 1)
        {
            return false;
        }

        var key = obj.Keys.First();
        if (key.Type != CBORType.TextString || !IsSpecialKey(key.AsString()))
        {
            return false;
        }

        var value = obj[key];
        if (value.Type != CBORType.TextString)
        {
            throw new DagCborEncodingException(
                $"Invalid DAG-CBOR: '{key.AsString()}' must be a string.");
        }

        WriteSpecialValue(writer, key.AsString(), value.AsString());
        return true;
    }

    private static void WriteSpecialValue(CborWriter writer, string name, string value)
    {
        if (name == LinkKey)
        {
            Cid cid;
            try
            {
                cid = Cid.FromString(value);
            }
            catch (Exception e)
            {
                throw new DagCborEncodingException($"Invalid DAG-CBOR: '{value}' is not a valid CID.", e);
            }

            // Tag 42 + byte string 0x00 || cid-bytes, exactly as Cid.ToCBORObject().
            WriteValue(writer, cid.ToCBORObject());
            return;
        }

        writer.WriteByteString(DecodeBase64(value));
    }

    private static byte[] DecodeBase64(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            // Tolerate unpadded / base64url encodings from non-canonical clients.
            var normalized = value.Replace('-', '+').Replace('_', '/');
            var padding = (4 - normalized.Length % 4) % 4;
            normalized = normalized.PadRight(normalized.Length + padding, '=');
            try
            {
                return Convert.FromBase64String(normalized);
            }
            catch (FormatException e)
            {
                throw new DagCborEncodingException("Invalid DAG-CBOR: '$bytes' is not valid base64.", e);
            }
        }
    }

    private class Utf8ByteComparer : IComparer<string>
    {
        public static readonly Utf8ByteComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x is null) return y is null ? 0 : -1;
            if (y is null) return 1;

            var xb = Encoding.UTF8.GetBytes(x);
            var yb = Encoding.UTF8.GetBytes(y);
            var len = Math.Min(xb.Length, yb.Length);
            for (var i = 0; i < len; i++)
            {
                var cmp = xb[i].CompareTo(yb[i]);
                if (cmp != 0) return cmp;
            }
            return xb.Length.CompareTo(yb.Length);
        }
    }
}
