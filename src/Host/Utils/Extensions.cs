using System.Text.Json;
using BlueNilePds.Core.CommonWeb;

namespace BlueNilePds.Host.Utils;

public static class Extensions
{
    extension(DidDocument document)
    {
        public JsonElement ToJsonElement()
        {
            return JsonSerializer.SerializeToElement(document);
        }
    }
}
