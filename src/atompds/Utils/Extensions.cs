using CommonWeb;
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
}