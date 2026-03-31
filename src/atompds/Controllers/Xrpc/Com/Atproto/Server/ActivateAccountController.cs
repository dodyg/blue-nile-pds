using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using CommonWeb;
using Microsoft.AspNetCore.Mvc;
using Sequencer;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class ActivateAccountController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<ActivateAccountController> _logger;
    private readonly SequencerRepository _sequencer;

    public ActivateAccountController(AccountRepository accountRepository,
        SequencerRepository sequencer,
        ILogger<ActivateAccountController> logger)
    {
        _accountRepository = accountRepository;
        _sequencer = sequencer;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.activateAccount")]
    [AccessFull]
    public async Task<IActionResult> ActivateAccountAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var requester = auth.AccessCredentials.Did;

        // TODO: assertValidDidDocumentForService

        var account = await _accountRepository.GetAccountAsync(requester, new AvailabilityFlags(false, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("User not found"));
        }

        await _accountRepository.ActivateAccountAsync(requester);

        // Sequence events for backwards compatibility
        var status = await _accountRepository.GetAccountStatusAsync(requester);
        await _sequencer.SequenceAccountEventAsync(requester, status);
        await _sequencer.SequenceIdentityEventAsync(requester, account.Handle ?? Constants.INVALID_HANDLE);

        return Ok();
    }
}
