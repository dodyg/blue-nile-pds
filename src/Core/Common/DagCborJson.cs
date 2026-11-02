using System.Text.Json;
using BlueNilePds.Core.CID;
using PeterO.Cbor;

namespace BlueNilePds.Core.Common;

/// <summary>
/// Converts decoded DAG-CBOR values into the atproto JSON data model:
/// tag-42 links become <c>{"$link": "..."}</c> and byte strings become
/// <c>{"$bytes": "..."}</c>. PeterO's <c>ToJSONString()</c> renders both as
/// base64url strings, which is not the atproto JSON form.
/// </summary>
public static class DagCborJson
{
    public static JsonElement ToJsonElement(CBORObject value)
    {
        var utf8 = ToUtf8Json(value);
        using var document = JsonDocument.Parse(utf8);
        return document.RootElement.Clone();
    }

    public static string ToJsonString(CBORObject value)
    {
        return JsonSerializer.Serialize(ToJsonElement(value));
    }

    public static byte[] ToUtf8Json(CBORObject value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteValue(writer, value);
        }

        return stream.ToArray();
    }

    private static void WriteValue(Utf8JsonWriter writer, CBORObject obj)
    {
        if (obj.IsTagged)
        {
            var tags = obj.GetAllTags().Select(t => (long)t).ToArray();
            var inner = obj.Untag();

            if (tags.Length == 1 && tags[0] == 42 && inner.Type == CBORType.ByteString)
            {
                Cid cid;
                try
                {
                    cid = Cid.FromCBOR(inner);
                }
                catch (Exception e)
                {
                    throw new DagCborEncodingException("Invalid DAG-CBOR: malformed CID link.", e);
                }

                writer.WriteStartObject();
                writer.WriteString("$link", cid.ToString());
                writer.WriteEndObject();
                return;
            }

            obj = inner;
        }

        switch (obj.Type)
        {
            case CBORType.Map:
                writer.WriteStartObject();
                foreach (var key in obj.Keys)
                {
                    writer.WritePropertyName(key.Type == CBORType.TextString ? key.AsString() : key.ToString());
                    WriteValue(writer, obj[key]);
                }
                writer.WriteEndObject();
                break;

            case CBORType.Array:
                writer.WriteStartArray();
                for (var i = 0; i < obj.Count; i++)
                {
                    WriteValue(writer, obj[i]);
                }
                writer.WriteEndArray();
                break;

            case CBORType.TextString:
                writer.WriteStringValue(obj.AsString());
                break;

            case CBORType.ByteString:
                writer.WriteStartObject();
                writer.WriteString("$bytes", Convert.ToBase64String(obj.GetByteString()));
                writer.WriteEndObject();
                break;

            case CBORType.Integer:
                writer.WriteRawValue(obj.AsNumber().ToString(), skipInputValidation: true);
                break;

            case CBORType.FloatingPoint:
                writer.WriteNumberValue(obj.AsDouble());
                break;

            case CBORType.Boolean:
                writer.WriteBooleanValue(obj.AsBoolean());
                break;

            default:
                if (obj.IsNull || obj.IsUndefined)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    throw new DagCborEncodingException($"CBORType.{obj.Type} cannot be represented in the atproto JSON data model");
                }
                break;
        }
    }
}
