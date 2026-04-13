using ActorStore;
using atompds.Middleware;
using Config;
using Crypto;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Identity;

public static class GetRecommendedDidCredentialsEndpoints
{
    public static RouteGroupBuilder MapGetRecommendedDidCredentialsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.identity.getRecommendedDidCredentials", Handle).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static IResult Handle(
        HttpContext context,
        ActorRepositoryProvider actorRepositoryProvider,
        SecretsConfig secretsConfig)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var signingKey = actorRepositoryProvider.KeyPair(did);
        var rotationKeyDid = secretsConfig.PlcRotationKey.Did();

        return Results.Ok(new
        {
            rotationKeys = new[] { rotationKeyDid },
            signingKey = signingKey.Did()
        });
    }
}
