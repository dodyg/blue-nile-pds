using System.Buffers.Text;
using SimpleBase;

namespace Crypto;

public class Multibase
{
    private static readonly Base16 Base16Lower = new(new Base16Alphabet("0123456789abcdef"));
    private static readonly Base16 Base16Upper = new(new Base16Alphabet("0123456789ABCDEF"));
    private static readonly Base32 Base32Lower = new(new Base32Alphabet("abcdefghijklmnopqrstuvwxyz234567"));
    private static readonly Base32 Base32Upper = new(new Base32Alphabet("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
    private static readonly Base58 Base58Btc = new(new Base58Alphabet("123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"));


    public static byte[] MultibaseToBytes(string mb)
    {
        var prefix = mb[0];
        var key = mb[1..];
        return prefix switch
        {
            'f' => Base16Lower.Decode(key),
            'F' => Base16Upper.Decode(key),
            'b' => Base32Lower.Decode(key),
            'B' => Base32Upper.Decode(key),
            'z' => Base58Btc.Decode(key),
            'm' => Convert.FromBase64String(key),
            // TODO: Need to validate base64url decoding
            'u' => Base64Url.DecodeFromChars(key),
            'U' => throw new NotImplementedException("base64urlpad"),
            _ => throw new ArgumentException($"Unsupported multibase: :{prefix}")
        };
    }

    public static string BytesToMultibase(byte[] bytes, string encoding)
    {
        return encoding switch
        {
            "base16" => $"f{Base16Lower.Encode(bytes)}",
            "base16upper" => $"F{Base16Upper.Encode(bytes)}",
            "base32" => $"b{Base32Lower.Encode(bytes)}",
            "base32upper" => $"B{Base32Upper.Encode(bytes)}",
            "base58btc" => $"z{Base58Btc.Encode(bytes)}",
            "base64" => $"m{Convert.ToBase64String(bytes)}",
            "base64url" => $"u{Base64Url.EncodeToChars(bytes)}",
            "base64urlpad" => throw new NotImplementedException("base64urlpad"),
            _ => throw new ArgumentException($"Unsupported encoding: {encoding}")
        };
    }
}