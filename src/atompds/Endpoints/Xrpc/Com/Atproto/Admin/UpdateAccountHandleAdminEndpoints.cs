using BlueNilePds.Pds.AccountManager;
using BlueNilePds.Host.Middleware;
using ComAtproto.Admin;
using BlueNilePds.Core.Handle;
using BlueNilePds.Pds.Sequencer;
using BlueNilePds.Pds.Xrpc;

namespace BlueNilePds.Host.Endpoints.Xrpc.Com.Atproto.Admin;

public static class UpdateAccountHandleAdminEndpoints
{
    public static RouteGroupBuilder MapUpdateAccountHandleAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.updateAccountHandle", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        UpdateAccountHandleInput request,
        AccountRepository accountRepository,
        HandleManager handleManager,
        SequencerRepository sequencer)
    {
        var did = (string)request.Did;
        if (string.IsNullOrWhiteSpace(did) || string.IsNullOrWhiteSpace(request.Handle))
            throw new XRPCError(new InvalidRequestErrorDetail("did and handle are required"));

        var handle = await handleManager.NormalizeAndValidateHandleAsync(request.Handle, did, false);
        await accountRepository.UpdateHandleAsync(did, handle);
        await sequencer.SequenceIdentityEventAsync(did, handle);

        return Results.Ok(new { });
    }
}
