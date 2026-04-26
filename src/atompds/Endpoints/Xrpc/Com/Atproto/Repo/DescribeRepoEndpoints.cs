using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Utils;
using CarpaNet;
using CommonWeb;
using ComAtproto.Repo;
using Identity;
using Xrpc;
using DidDoc = CommonWeb.DidDoc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Repo;

public static class DescribeRepoEndpoints
{
    public static RouteGroupBuilder MapDescribeRepoEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.repo.describeRepo", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        string repo,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        IdResolver idResolver,
        ILogger<Program> logger)
    {
        var account = await AssertRepoAvailabilityAsync(repo, accountRepository);

        DidDocument didDoc;
        try
        {
            didDoc = await idResolver.DidResolver.EnsureResolveAsync(account.Did);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to resolve DID: {Did}", account.Did);
            throw new XRPCError(new InvalidRequestErrorDetail($"Could not resolve DID: {account.Did}"));
        }

        var handle = DidDoc.GetHandle(didDoc);
        var responseHandle = handle ?? account.Handle ?? Constants.INVALID_HANDLE;
        var handleIsCorrect = handle == account.Handle;

        await using var actorDb = actorRepositoryProvider.Open(account.Did);
        var collections = await actorDb.Repo.GetCollectionsAsync();

        return Results.Ok(new DescribeRepoOutput
        {
            Handle = new ATHandle(responseHandle),
            Did = new ATDid(account.Did),
            DidDoc = didDoc.ToJsonElement(),
            Collections = collections.ToList(),
            HandleIsCorrect = handleIsCorrect
        });
    }

    private static async Task<ActorAccount> AssertRepoAvailabilityAsync(string did, AccountRepository accountRepository, bool isAdminOrSelf = false)
    {
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("RepoNotFound", $"Could not find repo for DID: {did}"));

        if (isAdminOrSelf) return account;

        if (account.TakedownRef != null)
            throw new XRPCError(new InvalidRequestErrorDetail("RepoTakedown", $"Repo has been takendown: {did}"));

        if (account.DeactivatedAt != null)
            throw new XRPCError(new InvalidRequestErrorDetail("RepoDeactivated", $"Repo has been deactivated: {did}"));

        return account;
    }
}
