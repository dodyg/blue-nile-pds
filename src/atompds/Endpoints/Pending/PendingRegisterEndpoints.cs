using atompds.Endpoints.Pending.Models;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using PendingAccounts.Services;

namespace atompds.Endpoints.Pending;

public static class PendingRegisterEndpoints
{
    public static RouteGroupBuilder MapPendingRegisterEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapGet("/validate-invite/{code}", ValidateInviteCodeAsync);
        return group;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] PendingRegisterRequest request,
        PendingAccountService pendingAccountService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Handle) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Email, handle, and password are required" });
        }

        var result = await pendingAccountService.RegisterAsync(
            request.Email, request.Handle, request.Password, request.InviteCode,
            request.Location, request.AccountType);

        if (!result.Success)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new
        {
            accessJwt = result.AccessToken,
            refreshJwt = result.RefreshToken
        });
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] PendingLoginRequest request,
        PendingAccountService pendingAccountService)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Identifier and password are required" });
        }

        var result = await pendingAccountService.LoginAsync(request.Identifier, request.Password);

        if (!result.Success)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new
        {
            accessJwt = result.AccessToken,
            refreshJwt = result.RefreshToken,
            status = result.Status
        });
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] PendingRefreshRequest request,
        PendingAccountService pendingAccountService)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshJwt))
            return Results.BadRequest(new { error = "Refresh token is required" });

        var tokens = await pendingAccountService.RefreshAsync(request.RefreshJwt);

        if (tokens == null)
            return Results.BadRequest(new { error = "Invalid or expired refresh token" });

        return Results.Ok(new
        {
            accessJwt = tokens.Value.AccessToken,
            refreshJwt = tokens.Value.RefreshToken
        });
    }

    private static async Task<IResult> ValidateInviteCodeAsync(
        string code,
        PendingAccountService pendingAccountService)
    {
        var result = await pendingAccountService.ValidateInviteCodeAsync(code);
        return Results.Ok(new { valid = result.Valid, error = result.Error });
    }
}
