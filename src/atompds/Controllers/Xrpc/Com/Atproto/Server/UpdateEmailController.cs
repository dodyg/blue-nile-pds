using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Services;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class UpdateEmailController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly EmailAddressValidator _emailAddressValidator;
    private readonly ILogger<UpdateEmailController> _logger;

    public UpdateEmailController(
        AccountRepository accountRepository,
        EmailAddressValidator emailAddressValidator,
        ILogger<UpdateEmailController> logger)
    {
        _accountRepository = accountRepository;
        _emailAddressValidator = emailAddressValidator;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.updateEmail")]
    [AccessPrivileged]
    public async Task<IActionResult> UpdateEmailAsync([FromBody] UpdateEmailInput request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("email is required"));
        }

        await _emailAddressValidator.AssertSupportedEmailAsync(request.Email);

        var existingAccount = await _accountRepository.GetAccountByEmailAsync(request.Email, new AvailabilityFlags(true, true));
        if (existingAccount != null && existingAccount.Did != did)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("This email address is already in use, please use a different email."));
        }

        if (account.EmailConfirmedAt != null)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                throw new XRPCError(new InvalidRequestErrorDetail("TokenRequired", "confirmation token required"));
            }

            await _accountRepository.AssertValidEmailTokenAsync(did, request.Token, EmailToken.EmailTokenPurpose.update_email);
        }

        await _accountRepository.UpdateEmailAsync(did, request.Email);

        return Ok();
    }
}

public class UpdateEmailInput
{
    public string? Email { get; set; }
    public bool? EmailAuthFactor { get; set; }
    public string? Token { get; set; }
}
