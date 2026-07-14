using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using CarpaNet;
using ComAtproto.Admin;
using Config;
using DidLib;
using Handle;
using Sequencer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

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
