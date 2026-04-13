using System.Text.Json;
using atompds.Middleware;
using Config;
using Crypto;
using Crypto.Secp256k1;
using DidLib;
using Microsoft.AspNetCore.Mvc;
using PeterO.Cbor;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Identity;

[ApiController]
[Route("xrpc")]
public class SignPlcOperationController : ControllerBase
{
    private readonly ILogger<SignPlcOperationController> _logger;
    private readonly SecretsConfig _secretsConfig;

    public SignPlcOperationController(SecretsConfig secretsConfig, ILogger<SignPlcOperationController> logger)
    {
        _secretsConfig = secretsConfig;
        _logger = logger;
    }

    [HttpPost("com.atproto.identity.signPlcOperation")]
    [AccessPrivileged]
    public async Task<IActionResult> SignPlcOperationAsync([FromBody] SignPlcOperationRequest request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (request.Operation == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("operation is required"));
        }

        var rotationKey = _secretsConfig.PlcRotationKey;
        var opJson = JsonSerializer.Serialize(request.Operation);
        var cborOp = CBORObject.FromJSONString(opJson);

        if (!cborOp.ContainsKey("prev") || cborOp["prev"].IsNull)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("operation must include a 'prev' field"));
        }

        var sigBytes = rotationKey.Sign(cborOp.EncodeToBytes());
        var sig = Convert.ToBase64String(sigBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var result = new Dictionary<string, object?>(request.Operation)
        {
            ["sig"] = sig
        };

        return Ok(result);
    }
}

public class SignPlcOperationRequest
{
    public Dictionary<string, object?>? Operation { get; set; }
}
