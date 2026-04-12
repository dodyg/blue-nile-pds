using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Config;
using DidLib;
using Handle;
using Microsoft.AspNetCore.Mvc;
using Sequencer;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class UpdateAccountHandleAdminController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly HandleManager _handleManager;
    private readonly ILogger<UpdateAccountHandleAdminController> _logger;
    private readonly PlcClient _plcClient;
    private readonly SecretsConfig _secretsConfig;
    private readonly SequencerRepository _sequencer;
    private readonly ServiceConfig _serviceConfig;

    public UpdateAccountHandleAdminController(
        AccountRepository accountRepository,
        HandleManager handleManager,
        PlcClient plcClient,
        SecretsConfig secretsConfig,
        ServiceConfig serviceConfig,
        SequencerRepository sequencer,
        ILogger<UpdateAccountHandleAdminController> logger)
    {
        _accountRepository = accountRepository;
        _handleManager = handleManager;
        _plcClient = plcClient;
        _secretsConfig = secretsConfig;
        _serviceConfig = serviceConfig;
        _sequencer = sequencer;
        _logger = logger;
    }

    [HttpPost("com.atproto.admin.updateAccountHandle")]
    [AdminToken]
    public async Task<IActionResult> UpdateAccountHandleAsync([FromBody] AdminUpdateHandleInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Did) || string.IsNullOrWhiteSpace(request.Handle))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did and handle are required"));
        }

        var handle = await _handleManager.NormalizeAndValidateHandleAsync(request.Handle, request.Did, false);
        await _accountRepository.UpdateHandleAsync(request.Did, handle);
        await _sequencer.SequenceIdentityEventAsync(request.Did, handle);

        return Ok();
    }
}

public class AdminUpdateHandleInput
{
    public string? Did { get; set; }
    public string? Handle { get; set; }
}
