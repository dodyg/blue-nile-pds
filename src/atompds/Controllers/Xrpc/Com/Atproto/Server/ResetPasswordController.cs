using AccountManager;
using AccountManager.Db;
using Microsoft.AspNetCore.Mvc;
using Scrypt;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class ResetPasswordController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<ResetPasswordController> _logger;

    public ResetPasswordController(AccountRepository accountRepository, ILogger<ResetPasswordController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.resetPassword")]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordInput request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("token and password are required"));
        }

        if (string.IsNullOrWhiteSpace(request.Did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        }

        await _accountRepository.AssertValidEmailTokenAsync(request.Did, request.Token, EmailToken.EmailTokenPurpose.reset_password);
        await _accountRepository.UpdatePasswordAsync(request.Did, request.Password);

        return Ok();
    }
}

public class ResetPasswordInput
{
    public string? Did { get; set; }
    public string? Token { get; set; }
    public string? Password { get; set; }
}
