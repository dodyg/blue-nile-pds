using Crypto.Secp256k1;
using SimpleBase;

namespace Crypto;

public class Did
{
    public static readonly Dictionary<string, IDidKeyPlugin> KeyPlugins = new()
    {
        { Const.SECP256K1_JWT_ALG, new Secp256k1Plugin() }
    };
    
    public static ParsedMultiKey ParseMultiKey(string multiKey)
    {
        var prefixedBytes = Utils.ExtractPrefixedBytes(multiKey);
        var plugin = KeyPlugins.Values.FirstOrDefault(x => Utils.HasPrefix(prefixedBytes, x.Prefix));
        if (plugin == null)
        {
            throw new ArgumentException("Unsupported key type");
        }
        
        var keyBytes = plugin.DecompressPubKey(prefixedBytes[plugin.Prefix.Length..]);
        return new ParsedMultiKey(plugin.JwtAlg, keyBytes);
    }
    
    public static ParsedMultiKey ParseDidKey(string did)
    {
        return ParseMultiKey(Utils.ExtractMultiKey(did));
    }
    
    public static string FormatDidKey(string jwtAlg, byte[] keyBytes)
    {
        return $"{Const.DID_KEY_PREFIX}:{FormatMultiKey(jwtAlg, keyBytes)}";
    }
    
    public static string FormatMultiKey(string jwtAlg, byte[] keyBytes)
    {
        if (!KeyPlugins.TryGetValue(jwtAlg, out var plugin))
        {
            throw new ArgumentException("Unsupported key type");
        }

        byte[] prefixedBytes =
        [
            ..plugin.Prefix,
            ..plugin.CompressPubKey(keyBytes)
        ];
        
        return $"{Const.BASE58_MULTIBASE_PREFIX}{Base58.Bitcoin.Encode(prefixedBytes)}";
    }
}

public record ParsedMultiKey(string JwtAlg, byte[] KeyBytes);