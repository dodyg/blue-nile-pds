using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Sequencer;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class AdminDeleteAccountController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<AdminDeleteAccountController> _logger;
    private readonly SequencerRepository _sequencer;

    public AdminDeleteAccountController(
        AccountRepository accountRepository,
        SequencerRepository sequencer,
        ILogger<AdminDeleteAccountController> logger)
    {
        _accountRepository = accountRepository;
        _sequencer = sequencer;
        _logger = logger;
    }

    [HttpPost("com.atproto.admin.deleteAccount")]
    [AdminToken]
    public async Task<IActionResult> DeleteAccountAsync([FromBody] AdminDeleteAccountInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        }

        await _accountRepository.DeleteAccountAsync(request.Did);
        return Ok();
    }
}

public class AdminDeleteAccountInput
{
    public string? Did { get; set; }
}
