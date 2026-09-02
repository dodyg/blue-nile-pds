using BlueNilePds.Middleware;
using Microsoft.AspNetCore.Mvc;
using PendingAccounts.Services;

namespace BlueNilePds.Endpoints.Pending;

public static class PendingProfileEndpoints
{
    public static RouteGroupBuilder MapPendingProfileEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/profile", GetProfileAsync).WithMetadata(new PendingAccessAttribute());
        group.MapPut("/profile", UpdateProfileAsync).WithMetadata(new PendingAccessAttribute());
        return group;
    }

    private static async Task<IResult> GetProfileAsync(
        HttpContext context,
        PendingAccountService pendingAccountService)
    {
        var pendingId = PendingHelper.GetPendingRegistrationId(context);
        if (pendingId == null) return Results.Unauthorized();

        var profile = await pendingAccountService.GetProfileAsync(pendingId.Value);
        if (profile == null) return Results.NotFound();

        return Results.Ok(profile);
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpContext context,
        [FromBody] UpdatePendingProfileRequest request,
        PendingAccountService pendingAccountService)
    {
        var pendingId = PendingHelper.GetPendingRegistrationId(context);
        if (pendingId == null) return Results.Unauthorized();

        var success = await pendingAccountService.UpdateProfileAsync(pendingId.Value, request);
        if (!success) return Results.NotFound();

        return Results.Ok();
    }
}
