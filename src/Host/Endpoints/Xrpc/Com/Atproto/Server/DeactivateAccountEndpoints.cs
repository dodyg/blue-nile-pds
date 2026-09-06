using BlueNilePds.Pds.AccountManager;
using BlueNilePds.Host.Middleware;
using ComAtproto.Server;
using BlueNilePds.Pds.Sequencer;

namespace BlueNilePds.Host.Endpoints.Xrpc.Com.Atproto.Server;

public static class DeactivateAccountEndpoints
{
    public static RouteGroupBuilder MapDeactivateAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.deactivateAccount", HandleAsync).WithMetadata(new AccessFullAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        DeactivateAccountInput? input,
        AccountRepository accountRepository,
        SequencerRepository sequencer)
    {
        var auth = context.GetAuthOutput();
        var requester = auth.AccessCredentials.Did;

        await accountRepository.DeactivateAccountAsync(requester, input?.DeleteAfter);

        var status = await accountRepository.GetAccountStatusAsync(requester);
        await sequencer.SequenceAccountEventAsync(requester, status);

        return Results.Ok();
    }
}
