using System.Security.Cryptography;
using SimpleBase;

namespace Crypto;

public static class Utils
{
    public static string Sha256Hex(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return hash.ToHex();
    }
    
    public static string ToHex(this byte[] bytes)
    {
        return Convert.ToHexString(bytes);
    }
    
    public static string ExtractMultiKey(string did)
    {
        if (!did.StartsWith(Const.DID_KEY_PREFIX))
        {
            throw new ArgumentException($"Incorrect prefix for did:key: {did}");
        }
        
        return did[Const.DID_KEY_PREFIX.Length..];
    }

    public static byte[] ExtractPrefixedBytes(string multiKey)
    {
        if (!multiKey.StartsWith(Const.BASE58_MULTIBASE_PREFIX))
        {
            throw new ArgumentException($"Incorrect prefix for multi-key: {multiKey}");
        }
        
        var sub = multiKey[Const.BASE58_MULTIBASE_PREFIX.Length..];
        var bytes = Base58.Bitcoin.Decode(sub);
        return bytes;
    }
    
    public static bool HasPrefix(byte[] bytes, byte[] prefix)
    {
        var prefixLength = prefix.Length;
        if (bytes.Length < prefixLength)
        {
            return false;
        }
        
        for (var i = 0; i < prefixLength; i++)
        {
            if (bytes[i] != prefix[i])
            {
                return false;
            }
        }
        
        return true;
    }
}