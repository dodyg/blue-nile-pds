using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class UpdateAccountPasswordAdminEndpoints
{
    public static RouteGroupBuilder MapUpdateAccountPasswordAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.updateAccountPassword", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(AdminUpdatePasswordInput request, AccountRepository accountRepository)
    {
        if (string.IsNullOrWhiteSpace(request.Did) || string.IsNullOrWhiteSpace(request.Password))
            throw new XRPCError(new InvalidRequestErrorDetail("did and password are required"));

        await accountRepository.UpdatePasswordAsync(request.Did, request.Password);
        return Results.Ok();
    }
}

public class AdminUpdatePasswordInput
{
    public string? Did { get; set; }
    public string? Password { get; set; }
}
