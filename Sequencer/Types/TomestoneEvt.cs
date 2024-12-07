using Common;
using PeterO.Cbor;

namespace Sequencer.Types;

public record TomestoneEvt : ICborEncodable
{
    public required string Did { get; init; }
    
    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("did", Did);
        return cbor;
    }
    
    public static TomestoneEvt FromCborObject(CBORObject cbor)
    {
        var did = cbor["did"].AsString();
        return new TomestoneEvt
        {
            Did = did
        };
    }
}