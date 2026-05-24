using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
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
        AdminUpdateHandleInput request,
        AccountRepository accountRepository,
        HandleManager handleManager,
        SequencerRepository sequencer)
    {
        if (string.IsNullOrWhiteSpace(request.Did) || string.IsNullOrWhiteSpace(request.Handle))
            throw new XRPCError(new InvalidRequestErrorDetail("did and handle are required"));

        var handle = await handleManager.NormalizeAndValidateHandleAsync(request.Handle, request.Did, false);
        await accountRepository.UpdateHandleAsync(request.Did, handle);
        await sequencer.SequenceIdentityEventAsync(request.Did, handle);

        return Results.Ok();
    }
}

public class AdminUpdateHandleInput
{
    public string? Did { get; set; }
    public string? Handle { get; set; }
}
