using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class CheckAccountStatusEndpoints
{
    public static RouteGroupBuilder MapCheckAccountStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.server.checkAccountStatus", HandleAsync).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        var (active, status) = AccountStore.FormatAccountStatus(account);

        string? repoRev = null, repoCid = null, repoRoot = null;

        if (active && actorRepositoryProvider.Exists(did))
        {
            await using var actorRepo = actorRepositoryProvider.Open(did);
            var root = await actorRepo.Repo.Storage.GetRootDetailedAsync();
            repoRev = root.Rev;
            repoCid = root.Cid.ToString();
            repoRoot = root.Cid.ToString();
        }

        return Results.Ok(new
        {
            active,
            status = status.ToString().ToLowerInvariant(),
            repoCommit = repoCid,
            repoRev,
            repoRoot
        });
    }
}
