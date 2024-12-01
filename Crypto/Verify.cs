namespace Crypto;

public class Verify
{
    public static bool VerifySignature(string didKey, byte[] data, byte[] sig, Types.VerifyOptions? opts, string? jwtAlg)
    {
        var parsed = Did.ParseDidKey(didKey);
        if (jwtAlg != null && parsed.JwtAlg != jwtAlg)
        {
            throw new ArgumentException($"Expected key alg {jwtAlg}, got {parsed.JwtAlg}");
        }
        
        var plugin = Did.KeyPlugins.Values.FirstOrDefault(x => x.JwtAlg == parsed.JwtAlg);
        if (plugin == null)
        {
            throw new ArgumentException($"Unsupported signature algorithm: {parsed.JwtAlg}");
        }
        
        return plugin.VerifySignature(didKey, data, sig, opts);
    }
}