using System.Text.Json;
using System.Text.Json.Serialization;
using Common;
using PeterO.Cbor;

namespace DidLib;

public class SignedOp<T> : ICborEncodable<SignedOp<T>> where T : ICborEncodable<T>
{
    [JsonPropertyName("sig")]
    public required string Sig { get; init; }
    
    [JsonPropertyName("op")]
    public required T Op { get; init; }
    
    public CBORObject ToCborObject()
    {
        var cbor = Op.ToCborObject();
        cbor.Add("sig", Sig);
        return cbor;
    }
    
    public static SignedOp<T> FromCborObject(CBORObject obj)
    {
        var op = T.FromCborObject(obj);
        var sig = obj["sig"].AsString();
        return new SignedOp<T>
        {
            Op = op,
            Sig = sig
        };
    }
}

public class Tombstone : ICborEncodable<Tombstone>
{
    [JsonPropertyName("type")]
    public string Type => "plc_tombstone";    
    
    [JsonPropertyName("prev")]
    public required string Prev { get; init; }
    
    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("type", Type);
        cbor.Add("prev", Prev);
        return cbor;
    }
    
    public static Tombstone FromCborObject(CBORObject cbor)
    {
        var prev = cbor["prev"].AsString();
        var type = cbor["type"].AsString();
        if (type != "plc_tombstone")
        {
            throw new Exception("Invalid type");
        }
        return new Tombstone
        {
            Prev = prev
        };
    }
}

public class AtProtoOp : ICborEncodable<AtProtoOp>
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
        
    // a CID hash pointer to a previous operation if an update, or null for a creation.
    // If null, the key should actually be part of the object, with value null, not simply omitted.
    // In DAG-CBOR encoding, the CID is string-encoded
    [JsonPropertyName("prev")]
    public required string? Prev { get; init; }
    
    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("type", Type);
        cbor.Add("verificationMethods", VerificationMethods);
        cbor.Add("rotationKeys", RotationKeys);
        cbor.Add("alsoKnownAs", AlsoKnownAs);
        cbor.Add("services", Services);
        cbor.Add("prev", Prev);
        return cbor;
    }
    
    public static AtProtoOp FromCborObject(CBORObject cbor)
    {
        var type = cbor["type"].AsString();
        var verificationMethods = JsonSerializer.Deserialize<Dictionary<string, string>>(cbor["verificationMethods"].ToJSONString());
        var rotationKeys = cbor["rotationKeys"].Values.Select(x => x.AsString()).ToArray();
        var alsoKnownAs = cbor["alsoKnownAs"].Values.Select(x => x.AsString()).ToArray();
        var services = JsonSerializer.Deserialize<Dictionary<string, Service>>(cbor["services"].ToJSONString());
        string? prev = cbor.ContainsKey("prev") && !cbor["prev"].IsNull ? cbor["prev"].AsString() : null;
        return new AtProtoOp
        {
            Type = type,
            VerificationMethods = verificationMethods ?? throw new Exception("Invalid verificationMethods"),
            RotationKeys = rotationKeys,
            AlsoKnownAs = alsoKnownAs,
            Services = services ?? throw new Exception("Invalid services"),
            Prev = prev
        };
    }
}