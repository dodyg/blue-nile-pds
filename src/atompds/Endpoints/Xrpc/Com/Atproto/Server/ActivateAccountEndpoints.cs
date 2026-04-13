using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using CommonWeb;
using Sequencer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class ActivateAccountEndpoints
{
    public static RouteGroupBuilder MapActivateAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.activateAccount", HandleAsync).WithMetadata(new AccessFullAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AccountRepository accountRepository,
        SequencerRepository sequencer)
    {
        var auth = context.GetAuthOutput();
        var requester = auth.AccessCredentials.Did;

        var account = await accountRepository.GetAccountAsync(requester, new AvailabilityFlags(false, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("User not found"));

        await accountRepository.ActivateAccountAsync(requester);

        var status = await accountRepository.GetAccountStatusAsync(requester);
        await sequencer.SequenceAccountEventAsync(requester, status);
        await sequencer.SequenceIdentityEventAsync(requester, account.Handle ?? Constants.INVALID_HANDLE);

        return Results.Ok();
    }
}
