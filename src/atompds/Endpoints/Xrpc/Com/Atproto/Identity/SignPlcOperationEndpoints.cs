using System.Text.Json;
using atompds.Middleware;
using Config;
using Crypto;
using DidLib;
using PeterO.Cbor;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Identity;

public static class SignPlcOperationEndpoints
{
    public static RouteGroupBuilder MapSignPlcOperationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.identity.signPlcOperation", Handle).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static IResult Handle(
        HttpContext context,
        SignPlcOperationRequest request,
        SecretsConfig secretsConfig)
    {
        var auth = context.GetAuthOutput();

        if (request.Operation == null)
            throw new XRPCError(new InvalidRequestErrorDetail("operation is required"));

        var rotationKey = secretsConfig.PlcRotationKey;
        var opJson = JsonSerializer.Serialize(request.Operation);
        var cborOp = CBORObject.FromJSONString(opJson);

        if (!cborOp.ContainsKey("prev") || cborOp["prev"].IsNull)
            throw new XRPCError(new InvalidRequestErrorDetail("operation must include a 'prev' field"));

        var sigBytes = rotationKey.Sign(cborOp.EncodeToBytes());
        var sig = Convert.ToBase64String(sigBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var result = new Dictionary<string, object?>(request.Operation)
        {
            ["sig"] = sig
        };

        return Results.Ok(result);
    }
}

public class SignPlcOperationRequest
{
    public Dictionary<string, object?>? Operation { get; set; }
}
