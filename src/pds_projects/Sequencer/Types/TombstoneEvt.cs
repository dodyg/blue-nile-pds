using Common;
using PeterO.Cbor;

namespace Sequencer.Types;

public record TombstoneEvt : ICborEncodable<TombstoneEvt>
{
    public required string Did { get; init; }

    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("did", Did);
        return cbor;
    }

    public static TombstoneEvt FromCborObject(CBORObject cbor)
    {
        var did = cbor["did"].AsString();
        return new TombstoneEvt
        {
            Did = did
        };
    }
}