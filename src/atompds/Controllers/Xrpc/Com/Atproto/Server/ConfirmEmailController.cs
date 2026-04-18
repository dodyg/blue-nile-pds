using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using ComAtproto.Server;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class ConfirmEmailController : ControllerBase
{
    private readonly AccountRepository _accountRepository;

    public ConfirmEmailController(AccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    [HttpPost("com.atproto.server.confirmEmail")]
    [AccessFull(true)]
    public async Task ConfirmEmailAsync([FromBody] ConfirmEmailInput input)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("AccountNotFound", "Account not found."));
        }

        var requestEmail = input.Email.ToLowerInvariant();
        if (account.Email != requestEmail)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("InvalidEmail", "Invalid email."));
        }

        await _accountRepository.ConfirmEmailAsync(did, input.Token);
    }
}
