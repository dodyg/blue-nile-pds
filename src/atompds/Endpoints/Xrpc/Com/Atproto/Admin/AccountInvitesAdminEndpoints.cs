using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class AccountInvitesAdminEndpoints
{
    public static RouteGroupBuilder MapAccountInvitesAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.enableAccountInvites", EnableAsync).WithMetadata(new AdminTokenAttribute());
        group.MapPost("com.atproto.admin.disableAccountInvites", DisableAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> EnableAsync(EnableInvitesInput request, AccountRepository accountRepository)
    {
        if (string.IsNullOrWhiteSpace(request.Did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));

        await accountRepository.UpdateInvitesDisabledAsync(request.Did, false);
        return Results.Ok();
    }

    private static async Task<IResult> DisableAsync(DisableInvitesInput request, AccountRepository accountRepository)
    {
        if (string.IsNullOrWhiteSpace(request.Did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));

        await accountRepository.UpdateInvitesDisabledAsync(request.Did, true);
        return Results.Ok();
    }
}

public class EnableInvitesInput
{
    public string? Did { get; set; }
}

public class DisableInvitesInput
{
    public string? Did { get; set; }
}
