using System.Text.Json.Serialization;
using PeterO.Cbor;

namespace DidLib;

public class SignedAtProtoOp : AtProtoOp
{
    [JsonPropertyName("sig")]
    public required string Signature { get; init; }
    
    public new CBORObject ToCborObject()
    {
        var cbor = base.ToCborObject();
        cbor.Add("sig", Signature);
        return cbor;
    }
}