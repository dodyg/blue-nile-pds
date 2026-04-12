using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Admin;

[ApiController]
[Route("xrpc")]
public class GetAccountInfosController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<GetAccountInfosController> _logger;

    public GetAccountInfosController(AccountRepository accountRepository, ILogger<GetAccountInfosController> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    [HttpGet("com.atproto.admin.getAccountInfos")]
    [AdminToken]
    public async Task<IActionResult> GetAccountInfosAsync([FromQuery] string dids)
    {
        var didList = dids.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var accounts = await _accountRepository.GetAccountsAsync(didList, new AvailabilityFlags(true, true));

        return Ok(new
        {
            accounts = accounts.Values.Select(a => new
            {
                did = a.Did,
                handle = a.Handle,
                email = a.Email,
                emailConfirmedAt = a.EmailConfirmedAt?.ToString("o"),
                takedownRef = a.TakedownRef,
                deactivatedAt = a.DeactivatedAt?.ToString("o"),
                createdAt = a.CreatedAt.ToString("o")
            })
        });
    }
}
