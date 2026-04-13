using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class CheckAccountStatusController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly ILogger<CheckAccountStatusController> _logger;

    public CheckAccountStatusController(
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        ILogger<CheckAccountStatusController> logger)
    {
        _accountRepository = accountRepository;
        _actorRepositoryProvider = actorRepositoryProvider;
        _logger = logger;
    }

    [HttpGet("com.atproto.server.checkAccountStatus")]
    [AccessPrivileged]
    public async Task<IActionResult> CheckAccountStatusAsync()
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));
        }

        var (active, status) = AccountStore.FormatAccountStatus(account);

        string? repoRev = null;
        string? repoCid = null;
        string? repoRoot = null;

        if (active && _actorRepositoryProvider.Exists(did))
        {
            await using var actorRepo = _actorRepositoryProvider.Open(did);
            var root = await actorRepo.Repo.Storage.GetRootDetailedAsync();
            repoRev = root.Rev;
            repoCid = root.Cid.ToString();
            repoRoot = root.Cid.ToString();
        }

        return Ok(new
        {
            active,
            status = status.ToString().ToLowerInvariant(),
            repoCommit = repoCid,
            repoRev,
            repoRoot
        });
    }
}
