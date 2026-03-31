using CarpaNet.Cbor;
using CarpaNet.Json;

namespace CommonWeb.Generated;

public static class CarpaNetGeneratedContexts
{
    public static ATProtoJsonContext Json => ATProtoJsonContext.Default;

    public static ATProtoCborContext Cbor => ATProtoCborContext.Default;

    public static CborSerializerContext CborSerializer => ATProtoCborContext.Default;
}
