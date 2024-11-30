using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Crypto;
using Dahomey.Cbor;
using SimpleBase;

namespace DidLib;

public class SignedAtProtoOp : AtProtoOp
{
    [JsonPropertyName("sig")]
    public required string Signature { get; init; }
}
    
public class AtProtoOp
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
        
    [JsonPropertyName("verificationMethods")]
    public required Dictionary<string, string> VerificationMethods { get; init; }
        
    [JsonPropertyName("rotationKeys")]
    public required string[] RotationKeys { get; init; }
        
    [JsonPropertyName("alsoKnownAs")]
    public required string[] AlsoKnownAs { get; init; }
        
    [JsonPropertyName("services")]
    public required Dictionary<string, Service> Services { get; init; }
        
    [JsonPropertyName("prev")]
    public required string? Prev { get; init; }
}

public record Service
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
        
    [JsonPropertyName("endpoint")]
    public required string Endpoint { get; init; }
}

public static class Operations
{



    public static async Task<string> GetSignature(object? obj, Types.IKeyPair keyPair)
    {
        using var memStr = new MemoryStream();
        await Cbor.SerializeAsync(obj, memStr);
        memStr.Seek(0, SeekOrigin.Begin);
        var memBuf = memStr.ToArray();
        var sig = keyPair.Sign(memBuf);
        var b64Url = Base64Url.EncodeToString(sig);
        return b64Url;
    }
    
    public static async Task<SignedAtProtoOp> AtProtoOp(string signingKey, string handle, string pds, string[] rotationKeys, string? cid, Types.IKeyPair keyPair)
    {
        var op = FormatAtProtoOp(signingKey, handle, pds, rotationKeys, cid);
        var sig = await GetSignature(op, keyPair);
        return new SignedAtProtoOp
        {
            Type = op.Type,
            VerificationMethods = op.VerificationMethods,
            RotationKeys = op.RotationKeys,
            AlsoKnownAs = op.AlsoKnownAs,
            Services = op.Services,
            Prev = op.Prev,
            Signature = sig
        };
    }

    public static async Task<(string Did, AtProtoOp Op)> CreateOp(string signingKey, string handle, string pds, string[] rotationKeys, Types.IKeyPair keyPair)
    {
        var op = await AtProtoOp(signingKey, handle, pds, rotationKeys, null, keyPair);
        var did = await DidForCreateOp(op);
        return (did, op);
    }

    public static async Task<string> DidForCreateOp(AtProtoOp op)
    {
        using var memStr = new MemoryStream();
        await Cbor.SerializeAsync(op, memStr);
        var hashOfGenesis = await SHA256.HashDataAsync(memStr);
        var hashB32 = Base32.Rfc4648.Encode(hashOfGenesis);
        var truncated = hashB32[..24];
        return $"did:plc:{truncated}";
    }

    public static AtProtoOp FormatAtProtoOp(string signingKey, string handle, string pds, string[] rotationKeys, string? cid)
    {
        return new AtProtoOp
        {
            Type = "plc_operation",
            VerificationMethods = new Dictionary<string, string>
            {
                {"atproto", signingKey}
            },
            RotationKeys = rotationKeys,
            AlsoKnownAs = [EnsureAtProtoPrefix(handle)],
            Services = new Dictionary<string, Service>
            {
                {"atproto_pds", new Service {Type = "AtprotoPersonalDataServer", Endpoint = EnsureHttpPrefix(pds)}}
            },
            Prev = cid
        };
    }
    
    private static string EnsureAtProtoPrefix(string str)
    {
        if (str.StartsWith("at://")) return str;
        var stripped = str.Replace("http://", "").Replace("https://", "");
        return $"at://{stripped}";
    }
    
    private static string EnsureHttpPrefix(string str)
    {
        if (str.StartsWith("http://") || str.StartsWith("https://")) return str;
        return $"https://{str}";
    }
}