using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class SubjectStatusController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<SubjectStatusController> _logger;

    public SubjectStatusController(AccountRepository accountRepository, ILogger<SubjectStatusController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpGet("com.atproto.admin.getSubjectStatus")]
    [AdminToken]
    public async Task<IActionResult> GetSubjectStatusAsync([FromQuery] string did)
    {
        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        var (active, status) = AccountStore.FormatAccountStatus(account);
        return Ok(new
        {
            subject = new { did },
            takedown = account.TakedownRef != null ? new { id = account.TakedownRef } : null,
            deactivated = account.DeactivatedAt != null
        });
    }

    [HttpPost("com.atproto.admin.updateSubjectStatus")]
    [AdminToken]
    public async Task<IActionResult> UpdateSubjectStatusAsync([FromBody] UpdateSubjectStatusInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        }

        var account = await _accountRepository.GetAccountAsync(request.Did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        if (request.Takedown != null)
        {
            await _accountRepository.UpdateTakedownRefAsync(request.Did, request.Takedown.Applied ? "admin-takedown" : null);
        }

        return Ok();
    }
}

public class UpdateSubjectStatusInput
{
    public string? Did { get; set; }
    public TakedownRef? Takedown { get; set; }
}

public class TakedownRef
{
    public bool Applied { get; set; }
}
