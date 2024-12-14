using Common;
using PeterO.Cbor;

namespace Sequencer.Types;

public record IdentityEvt : ICborEncodable<IdentityEvt>
{
    public required string Did { get; init; }
    public required string? Handle { get; init; }
    
    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("did", Did);
        if (Handle != null)
        {
            cbor.Add("handle", Handle);
        }
        return cbor;
    }
    
    public static IdentityEvt FromCborObject(CBORObject cbor)
    {
        var did = cbor["did"].AsString();
        var handle = cbor["handle"].AsString();
        return new IdentityEvt
        {
            Did = did,
            Handle = handle
        };
    }
}