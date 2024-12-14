using CommonWeb;
using DidDoc = FishyFlip.Models.DidDoc;
using Service = FishyFlip.Models.Service;
using VerificationMethod = FishyFlip.Models.VerificationMethod;

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

    public static VerificationMethod ToVerificationMethod(this CommonWeb.VerificationMethod method)
    {
        return new VerificationMethod(method.Id, method.Type, method.Controller, method.PublicKeyMultibase!);
    }

    public static Service ToService(this CommonWeb.Service method)
    {
        return new Service(method.Id, method.Type, method.ServiceEndpoint);
    }
}