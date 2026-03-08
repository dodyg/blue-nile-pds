using AccountManager;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Sequencer;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class DeactivateAccountController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<DeactivateAccountController> _logger;
    private readonly SequencerRepository _sequencer;

    public DeactivateAccountController(AccountRepository accountRepository,
        SequencerRepository sequencer,
        ILogger<DeactivateAccountController> logger)
    {
        _accountRepository = accountRepository;
        _sequencer = sequencer;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.deactivateAccount")]
    [AccessFull]
    public async Task<IActionResult> DeactivateAccount([FromBody] DeactivateAccountInput? input)
    {
        var auth = HttpContext.GetAuthOutput();
        var requester = auth.AccessCredentials.Did;

        await _accountRepository.DeactivateAccount(requester, input?.DeleteAfter);

        var status = await _accountRepository.GetAccountStatus(requester);
        await _sequencer.SequenceAccountEvent(requester, status);

        return Ok();
    }
}

public class DeactivateAccountInput
{
    public string? DeleteAfter { get; set; }
}
