using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class UpdateAccountEmailAdminController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<UpdateAccountEmailAdminController> _logger;

    public UpdateAccountEmailAdminController(AccountRepository accountRepository, ILogger<UpdateAccountEmailAdminController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpPost("com.atproto.admin.updateAccountEmail")]
    [AdminToken]
    public async Task<IActionResult> UpdateAccountEmailAsync([FromBody] AdminUpdateEmailInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Did) || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did and email are required"));
        }

        await _accountRepository.UpdateEmailAsync(request.Did, request.Email);
        return Ok();
    }
}

public class AdminUpdateEmailInput
{
    public string? Did { get; set; }
    public string? Email { get; set; }
}
