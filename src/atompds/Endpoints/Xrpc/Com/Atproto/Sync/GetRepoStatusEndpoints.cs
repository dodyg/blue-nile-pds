using AccountManager;
using AccountManager.Db;
using ActorStore;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class GetRepoStatusEndpoints
{
    public static RouteGroupBuilder MapGetRepoStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.getRepoStatus", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        string did)
    {
        var account = await accountRepository.GetAccountAsync(did, new(true, true));

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
            var root = await actorRepo.Repo.Storage.GetRootDetailedAsync();
            rev = root.Rev;
        }

        return Results.Ok(new { did, active, status = status.ToString(), rev });
    }
}
