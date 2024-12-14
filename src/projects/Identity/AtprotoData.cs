using CommonWeb;
using Crypto;

namespace Identity;

public class Atproto_Data
{
    public static string? GetKey(DidDocument doc)
    {
        var key = DidDoc.GetSigningKey(doc);
        if (key == null)
        {
            return null;
        }
        return GetDidKeyFromMultibase(key.Value.type, key.Value.publicKeyMultibase);
    }

    public static string? GetDidKeyFromMultibase(string type, string publicKeyMultibase)
    {
        var keyBytes = Multibase.MultibaseToBytes(publicKeyMultibase);
        if (type == "EcdsaSecp256r1VerificationKey2019")
        {
            return Did.FormatDidKey(Const.P256_JWT_ALG, keyBytes);
        }
        if (type == "EcdsaSecp256k1VerificationKey2019")
        {
            return Did.FormatDidKey(Const.SECP256K1_JWT_ALG, keyBytes);
        }
        if (type == "Multikey")
        {
            var parsed = Did.ParseMultiKey(publicKeyMultibase);
            return Did.FormatDidKey(parsed.JwtAlg, parsed.KeyBytes);
        }

        return null;
    }

    private static AtprotoData ParseAtProtoDocument(DidDocument doc)
    {
        // TODO: DidDocument.ToString 
        var did = DidDoc.GetDid(doc);
        var key = GetKey(doc);
        var handle = DidDoc.GetHandle(doc);
        var pdsEndpoint = DidDoc.GetPdsEndpoint(doc);
        return new AtprotoData(did, key!, handle!, pdsEndpoint!);
    }

    public static AtprotoData EnsureAtpDocument(DidDocument doc)
    {
        var atp = ParseAtProtoDocument(doc);
        if (atp.Did == null)
        {
            throw new Exception($"Could not parse id from doc: {doc}");
        }
        if (atp.SigningKey == null)
        {
            throw new Exception($"Could not parse key from doc: {doc}");
        }
        if (atp.Handle == null)
        {
            throw new Exception($"Could not parse handle from doc: {doc}");
        }
        if (atp.Pds == null)
        {
            throw new Exception($"Could not parse pdsEndpoint from doc: {doc}");
        }
        return atp;
    }

    public static string EnsureAtprotoKey(DidDocument doc)
    {
        var atDoc = ParseAtProtoDocument(doc);
        if (atDoc.SigningKey == null)
        {
            throw new Exception($"Could not parse signing key from doc: {doc}");
        }
        return atDoc.SigningKey;
    }
}