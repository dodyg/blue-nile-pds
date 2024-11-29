using System.Security.Cryptography;

namespace atompds.OauthProvider.Util;

public class Crypto
{
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
    
    public static string RandomHexId(int bytesLength = 16)
    {
        var buffer = new byte[bytesLength];
        Rng.GetBytes(buffer);
        return Convert.ToHexString(buffer);
    }
}