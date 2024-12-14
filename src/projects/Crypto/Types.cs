namespace Crypto;

public interface IKeyPair : ISigner, IDidable;

public interface IExportableKeyPair : IKeyPair
{
    public byte[] Export();
}

public interface ISigner
{
    public string JwtAlg { get; }
    public byte[] Sign(byte[] data);
}

public interface IDidable
{
    public string Did();
}

public interface IDidKeyPlugin
{
    public byte[] Prefix { get; }
    public string JwtAlg { get; }
    public bool VerifySignature(string did, byte[] msg, byte[] data, VerifyOptions? options);
    public byte[] CompressPubKey(byte[] uncompressed);
    public byte[] DecompressPubKey(byte[] compressed);
}

public record VerifyOptions
{
    public bool? AllowMalleableSig { get; init; }
}