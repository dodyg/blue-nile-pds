using AccountManager;
using AccountManager.Db;
using ActorStore;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[Route("xrpc")]
[ApiController]
public class GetRepoStatusController(
    AccountRepository accountRepository,
    ActorRepositoryProvider actorRepositoryProvider
) : ControllerBase
{
    [HttpGet("com.atproto.sync.getRepoStatus")]
    public async Task<IActionResult> GetRepoStatus(
        [FromQuery] string did
    )
    {
        var account = await accountRepository.GetAccount(did, new(true, true));

        if (account is null)
            throw new XRPCError(new InvalidRequestErrorDetail($"could not find account for did: {did}"));

        if (account.TakedownRef is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is taken down"));

        if (account.DeactivatedAt is not null)
            throw new XRPCError(new InvalidRequestErrorDetail($"account for did: {did} is deactivated"));

        var (active, status) = AccountStore.FormatAccountStatus(account);

        string? rev = null;
        if (active)
        {
            await using var actorRepo = actorRepositoryProvider.Open(did);
            var root = await actorRepo.Repo.Storage.GetRootDetailed();
            rev = root.Rev;
        }

        return Ok(new
        {
            did,
            active,
            status = status.ToString(),
            rev
        });
    }
}