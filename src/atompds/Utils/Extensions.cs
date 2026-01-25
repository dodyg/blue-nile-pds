using System.Text.Json;
using CommonWeb;
using FishyFlip.Lexicon;
using FishyFlip.Models;
using DidDoc = FishyFlip.Models.DidDoc;
using Service = FishyFlip.Models.Service;
using VerificationMethod = FishyFlip.Models.VerificationMethod;

namespace atompds.Utils;

public static class Extensions
{
    extension(DidDocument document)
    {
        public DidDoc ToDidDoc()
        {
            // TODO: Context?
            return new DidDoc([],
                document.Id,
                document.AlsoKnownAs?.ToList() ?? [],
                document.VerificationMethod?.Select(method => method.ToVerificationMethod()).ToList() ?? [],
                document.Service?.Select(service => service.ToService()).ToList() ?? []);
        }   
    }

    extension(CommonWeb.VerificationMethod method)
    {
        public VerificationMethod ToVerificationMethod()
        {
            return new VerificationMethod(method.Id, method.Type, method.Controller, method.PublicKeyMultibase!);
        }
    }   

    extension(CommonWeb.Service method)
    {
        public  Service ToService()
        {
            return new Service(method.Id, method.Type, method.ServiceEndpoint);
        }
    }

    extension(ATObject aTObject)
    {
        /// <summary>
        ///    Normalizes an ATObject to an object suitable for JSON serialization. <br/>
        ///    Avoids issues with UnknownATObject serialization as it uses the serilization of the underlying CBORObject. <br/>
        ///    use it when returning ATObject in API responses. <br/>
        ///    used along side options.JsonSerializerOptions.Converters.Add(new FishyFlip.Tools.Json.ATObjectJsonConverter()); <br/>
        /// </summary>
        /// <returns></returns>
        public object ToObjectResult()
        {
            if (aTObject is UnknownATObject uao)
            {
                return JsonSerializer.Deserialize<JsonElement>(uao.ToJson());
            }
            return aTObject;
        }
    }
}