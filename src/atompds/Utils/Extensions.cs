using System.Text.Json;
using CommonWeb;

namespace atompds.Utils;

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
