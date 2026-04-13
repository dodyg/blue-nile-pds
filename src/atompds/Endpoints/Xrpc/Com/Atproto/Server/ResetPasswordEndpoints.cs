using AccountManager;
using AccountManager.Db;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class ResetPasswordEndpoints
{
    public static RouteGroupBuilder MapResetPasswordEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.resetPassword", HandleAsync).RequireRateLimiting("auth-sensitive");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        ResetPasswordInput request,
        AccountRepository accountRepository)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
            throw new XRPCError(new InvalidRequestErrorDetail("token and password are required"));

        if (request.Password.Length < 8)
            throw new XRPCError(new InvalidRequestErrorDetail("Password must be at least 8 characters"));

        if (string.IsNullOrWhiteSpace(request.Did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));

        await accountRepository.AssertValidEmailTokenAsync(request.Did, request.Token, EmailToken.EmailTokenPurpose.reset_password);
        await accountRepository.UpdatePasswordAsync(request.Did, request.Password);

        return Results.Ok();
    }
}

public class ResetPasswordInput
{
    public string? Did { get; set; }
    public string? Token { get; set; }
    public string? Password { get; set; }
}
