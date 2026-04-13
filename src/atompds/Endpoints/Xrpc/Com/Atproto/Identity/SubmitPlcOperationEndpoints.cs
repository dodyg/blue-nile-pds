using System.Text.Json;
using atompds.Middleware;
using DidLib;
using PeterO.Cbor;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Identity;

public static class SubmitPlcOperationEndpoints
{
    public static RouteGroupBuilder MapSubmitPlcOperationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.identity.submitPlcOperation", HandleAsync).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        SubmitPlcOperationRequest request,
        PlcClient plcClient,
        ILogger<Program> logger)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (request.Operation == null)
            throw new XRPCError(new InvalidRequestErrorDetail("operation is required"));

        try
        {
            var opJson = JsonSerializer.Serialize(request.Operation);
            var cborOp = CBORObject.FromJSONString(opJson);

            var sig = request.Operation.TryGetValue("sig", out var sigObj) ? sigObj?.ToString() : null;
            if (sig == null)
                throw new XRPCError(new InvalidRequestErrorDetail("operation must include a 'sig' field"));

            var op = AtProtoOp.FromCborObject(cborOp);
            var signedOp = new SignedOp<AtProtoOp> { Op = op, Sig = sig };

            await plcClient.SendOperationAsync(did, signedOp);
        }
        catch (XRPCError)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to submit PLC operation for {did}", did);
            throw new XRPCError(new InvalidRequestErrorDetail("Failed to submit PLC operation"));
        }

        return Results.Ok();
    }
}

public class SubmitPlcOperationRequest
{
    public Dictionary<string, object?>? Operation { get; set; }
}
