using Common;
using PeterO.Cbor;

namespace Sequencer.Types;

public record HandleEvt : ICborEncodable
{
    public required string Did { get; init; }
    public required string Handle { get; init; }


    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("did", Did);
        cbor.Add("handle", Handle);
        return cbor;
    }

    public static HandleEvt FromCborObject(CBORObject cbor)
    {
        var did = cbor["did"].AsString();
        var handle = cbor["handle"].AsString();
        return new HandleEvt
        {
            Did = did,
            Handle = handle
        };
    }
}