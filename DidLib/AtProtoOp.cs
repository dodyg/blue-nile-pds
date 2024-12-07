using System.Text.Json.Serialization;
using Common;
using PeterO.Cbor;

namespace DidLib;

public class SignedTomestone : Tomestone
{
    [JsonPropertyName("sig")]
    public required string Sig { get; init; }
    
    public new CBORObject ToCborObject()
    {
        var cbor = base.ToCborObject();
        cbor.Add("sig", Sig);
        return cbor;
    }
}

public class Tomestone : ICborEncodable
{
    [JsonPropertyName("type")]
    public string Type => "plc_tomestone";    
    
    [JsonPropertyName("prev")]
    public required string Prev { get; init; }
    
    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("type", Type);
        cbor.Add("prev", Prev);
        return cbor;
    }
}

public class AtProtoOp : ICborEncodable
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
}