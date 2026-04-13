using System.Text;
using System.Text.Json;
using ActorStore;
using Config;
using Crypto;
using Jose;

namespace atompds.Services;

public class ServiceJwtBuilder
{
    private readonly ActorRepositoryProvider _actorRepositoryProvider;

    public ServiceJwtBuilder(ActorRepositoryProvider actorRepositoryProvider)
    {
        _actorRepositoryProvider = actorRepositoryProvider;
    }

    public string CreateServiceJwt(string did, string audience, string? lxm, long? exp = null)
    {
        var signingKeyPair = _actorRepositoryProvider.KeyPair(did, true);
        if (signingKeyPair is not IExportableKeyPair exportable)
        {
            throw new Exception("Signing key is not exportable");
        }

        return BuildJwt(new ServiceJwtPayload(did, audience, null, exp, lxm, exportable));
    }

    private static string BuildJwt(ServiceJwtPayload payload)
    {
        var iat = payload.iat ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp = payload.exp ?? DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var jti = Crypto.Utils.RandomHexString(16);
        var header = new
        {
            typ = "JWT",
            alg = payload.KeyPair.JwtAlg
        };
        var values = new Dictionary<string, object?>
        {
            ["iat"] = iat,
            ["iss"] = payload.iss,
            ["aud"] = payload.aud,
            ["exp"] = exp,
            ["lxm"] = payload.lxm,
            ["jti"] = jti
        };
        var pl = values
            .Where(kv => kv.Value != null)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var toSignStr = $"{Base64Url.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)))}." +
                        $"{Base64Url.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(pl)))}";
        var toSign = Encoding.UTF8.GetBytes(toSignStr);
        var sig = payload.KeyPair.Sign(toSign);
        return $"{toSignStr}.{Base64Url.Encode(sig)}";
    }

    private record ServiceJwtPayload(string iss, string aud, long? iat, long? exp, string? lxm, IExportableKeyPair KeyPair);
}
