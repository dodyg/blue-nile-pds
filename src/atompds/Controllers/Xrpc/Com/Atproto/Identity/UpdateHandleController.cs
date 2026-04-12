using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Config;
using DidLib;
using Handle;
using Microsoft.AspNetCore.Mvc;
using Sequencer;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Identity;

[ApiController]
[Route("xrpc")]
public class UpdateHandleController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly HandleManager _handleManager;
    private readonly ILogger<UpdateHandleController> _logger;
    private readonly PlcClient _plcClient;
    private readonly SecretsConfig _secretsConfig;
    private readonly SequencerRepository _sequencer;
    private readonly ServiceConfig _serviceConfig;

    public UpdateHandleController(
        AccountRepository accountRepository,
        HandleManager handleManager,
        PlcClient plcClient,
        SecretsConfig secretsConfig,
        ServiceConfig serviceConfig,
        SequencerRepository sequencer,
        ILogger<UpdateHandleController> logger)
    {
        _accountRepository = accountRepository;
        _handleManager = handleManager;
        _plcClient = plcClient;
        _secretsConfig = secretsConfig;
        _serviceConfig = serviceConfig;
        _sequencer = sequencer;
        _logger = logger;
    }

    [HttpPost("com.atproto.identity.updateHandle")]
    [AccessPrivileged]
    public async Task<IActionResult> UpdateHandleAsync([FromBody] UpdateHandleRequest request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Handle))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("handle is required"));
        }

        var validatedHandle = await _handleManager.NormalizeAndValidateHandleAsync(request.Handle, did, false);

        try
        {
            var signingKeyDid = _secretsConfig.PlcRotationKey.Did();
            var op = await Operations.AtProtoOpAsync(
                signingKeyDid,
                validatedHandle,
                _serviceConfig.PublicUrl,
                [_secretsConfig.PlcRotationKey.Did()],
                null,
                _secretsConfig.PlcRotationKey);
            await _plcClient.SendOperationAsync(did, op);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update PLC handle for {did}", did);
            throw new XRPCError(new InvalidRequestErrorDetail("Failed to update PLC handle"), e);
        }

        await _accountRepository.UpdateHandleAsync(did, validatedHandle);
        await _sequencer.SequenceIdentityEventAsync(did, validatedHandle);

        return Ok();
    }
}

public class UpdateHandleRequest
{
    public string? Handle { get; set; }
}
