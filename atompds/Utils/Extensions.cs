using CommonWeb;
using DidDoc = FishyFlip.Models.DidDoc;

namespace atompds.Utils;

public static class Extensions
{
    public static DidDoc ToDidDoc(this DidDocument document)
    {
        // TODO: Context?
        return new DidDoc([],
            document.Id,
            document.AlsoKnownAs?.ToList() ?? [],
            document.VerificationMethod?.Select(method => method.ToVerificationMethod()).ToList() ?? [],
            document.Service?.Select(service => service.ToService()).ToList() ?? []);
    }
    
    public static FishyFlip.Models.VerificationMethod ToVerificationMethod(this VerificationMethod method)
    {
        return new FishyFlip.Models.VerificationMethod(method.Id, method.Type, method.Controller, method.PublicKeyMultibase!);
    }
    
    public static FishyFlip.Models.Service ToService(this Service method)
    {
        return new FishyFlip.Models.Service(method.Id, method.Type, method.ServiceEndpoint);
    }
}