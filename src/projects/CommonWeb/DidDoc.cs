namespace CommonWeb;

public class DidDoc
{
    public static string GetDid(DidDocument doc)
    {
        return doc.Id;
    }

    public static string? GetHandle(DidDocument doc)
    {
        if (doc.AlsoKnownAs != null)
        {
            foreach (var alias in doc.AlsoKnownAs)
            {
                if (alias.StartsWith("at://"))
                {
                    // strip off "at://" prefix
                    return alias[5..];
                }
            }
        }

        return null;
    }

    public static (string type, string publicKeyMultibase)? GetSigningKey(DidDocument doc)
    {
        return GetVerificationMaterial(doc, "atproto");
    }

    public static string? GetSigningDidKey(DidDocument doc)
    {
        var parsed = GetSigningKey(doc);
        if (parsed == null)
        {
            return null;
        }

        return $"did:key:{parsed.Value.publicKeyMultibase}";
    }

    public static (string type, string publicKeyMultibase)? GetVerificationMaterial(DidDocument doc, string keyId)
    {
        VerificationMethod? match = null;
        if (doc.VerificationMethod != null)
        {
            var keyIdStr = $"#{keyId}";
            var docIdKeyId = $"{doc.Id}#{keyId}";
            foreach (var verificationMethod in doc.VerificationMethod)
            {
                var itemId = verificationMethod.Id;
                if (itemId[0] == '#')
                {
                    // itemId == $"#{keyId}"
                    if (itemId == keyIdStr)
                    {
                        match = verificationMethod;
                        break;
                    }
                }
                else
                {
                    // itemId == $"{doc.Id}#{keyId}"
                    if (itemId == docIdKeyId)
                    {
                        match = verificationMethod;
                        break;
                    }
                }
            }
        }

        if (match?.PublicKeyMultibase == null)
        {
            return null;
        }

        return (match.Type, match.PublicKeyMultibase);
    }

    public static string? GetPdsEndpoint(DidDocument doc)
    {
        return GetServiceEndpoint(doc, "atproto_pds", "AtprotoPersonalDataServer");
    }

    public static string? GetServiceEndpoint(DidDocument doc, string id, string? type)
    {
        var service = GetService(doc, id);
        if (service == null)
        {
            return null;
        }

        if (type != null && service.Type != type)
        {
            return null;
        }

        return ValidateUrl(service.ServiceEndpoint);
    }

    public static string? ValidateUrl(string url)
    {
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            return null;
        }

        if (!CanParseUrl(url))
        {
            return null;
        }

        return url;
    }

    public static bool CanParseUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }

    public static Service? GetService(DidDocument doc, string id)
    {
        Service? service = null;
        if (doc.Service != null)
        {
            var keyIdStr = $"#{id}";
            var docIdKeyId = $"{doc.Id}#{id}";
            foreach (var svc in doc.Service)
            {
                var itemId = svc.Id;
                if (itemId[0] == '#')
                {
                    // itemId == $"#{id}"
                    if (itemId == keyIdStr)
                    {
                        service = svc;
                        break;
                    }
                }
                else
                {
                    // itemId == $"{doc.Id}#{id}"
                    if (itemId == docIdKeyId)
                    {
                        service = svc;
                        break;
                    }
                }
            }
        }

        if (service == null)
        {
            return null;
        }

        return service;
    }
}