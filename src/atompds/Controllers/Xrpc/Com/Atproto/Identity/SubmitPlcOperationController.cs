using System.Text.Json;
using atompds.Middleware;
using DidLib;
using Microsoft.AspNetCore.Mvc;
using PeterO.Cbor;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Identity;

[ApiController]
[Route("xrpc")]
public class SubmitPlcOperationController : ControllerBase
{
    private readonly ILogger<SubmitPlcOperationController> _logger;
    private readonly PlcClient _plcClient;

    public SubmitPlcOperationController(PlcClient plcClient, ILogger<SubmitPlcOperationController> logger)
    {
        _plcClient = plcClient;
        _logger = logger;
    }

    [HttpPost("com.atproto.identity.submitPlcOperation")]
    [AccessPrivileged]
    public async Task<IActionResult> SubmitPlcOperationAsync([FromBody] SubmitPlcOperationRequest request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (request.Operation == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("operation is required"));
        }

        try
        {
            var opJson = JsonSerializer.Serialize(request.Operation);
            var cborOp = CBORObject.FromJSONString(opJson);

            var sig = request.Operation.TryGetValue("sig", out var sigObj) ? sigObj?.ToString() : null;
            if (sig == null)
            {
                throw new XRPCError(new InvalidRequestErrorDetail("operation must include a 'sig' field"));
            }

            var op = AtProtoOp.FromCborObject(cborOp);
            var signedOp = new SignedOp<AtProtoOp>
            {
                Op = op,
                Sig = sig
            };

            await _plcClient.SendOperationAsync(did, signedOp);
        }
        catch (XRPCError)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to submit PLC operation for {did}", did);
            throw new XRPCError(new InvalidRequestErrorDetail("Failed to submit PLC operation"));
        }

        return Ok();
    }
}

public class SubmitPlcOperationRequest
{
    public Dictionary<string, object?>? Operation { get; set; }
}
