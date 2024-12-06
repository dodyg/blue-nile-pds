namespace CID;

public enum Version
{
    V0 = 0,
    V1 = 1
}

public enum Error
{
    UnknownCodec,
    InputTooShort,
    InvalidCidVersion,
    InvalidCidV0Codec,
    InvalidCidV0Multihash,
    InvalidCidV0Base,
    InvalidExplicitCidV0
}

public class CIDException : Exception
{
    public Error Error { get; }
    public CIDException(Error error) : base(error.ToString())
    {
        Error = error;
    }
}