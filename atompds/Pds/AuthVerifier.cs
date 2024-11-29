using System.Security.Cryptography;
using System.Text;

namespace atompds.Pds;

public class AuthVerifier
{
    public static string GenerateJwtSecretKey(string secret)
    {
        using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(secret));
        return Convert.ToBase64String(hmac.Key);
    }
}