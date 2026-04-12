using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class ListAppPasswordsController : ControllerBase
{
    private readonly AppPasswordStore _appPasswordStore;
    private readonly ILogger<ListAppPasswordsController> _logger;

    public ListAppPasswordsController(AppPasswordStore appPasswordStore, ILogger<ListAppPasswordsController> logger)
    {
        _appPasswordStore = appPasswordStore;
        _logger = logger;
    }

    [HttpGet("com.atproto.server.listAppPasswords")]
    [AccessStandard]
    public async Task<IActionResult> ListAppPasswordsAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var passwords = await _appPasswordStore.ListAppPasswordsAsync(did);

        return Ok(new
        {
            passwords = passwords.Select(ap => new
            {
                name = ap.Name,
                createdAt = ap.CreatedAt.ToString("o"),
                privileged = ap.Privileged
            })
        });
    }
}
