using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class UpdateAccountPasswordAdminController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<UpdateAccountPasswordAdminController> _logger;

    public UpdateAccountPasswordAdminController(AccountRepository accountRepository, ILogger<UpdateAccountPasswordAdminController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpPost("com.atproto.admin.updateAccountPassword")]
    [AdminToken]
    public async Task<IActionResult> UpdateAccountPasswordAsync([FromBody] AdminUpdatePasswordInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Did) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did and password are required"));
        }

        await _accountRepository.UpdatePasswordAsync(request.Did, request.Password);
        return Ok();
    }
}

public class AdminUpdatePasswordInput
{
    public string? Did { get; set; }
    public string? Password { get; set; }
}
