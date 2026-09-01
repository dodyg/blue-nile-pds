using atompds.Endpoints.Pending.Models;
using atompds.Middleware;
using atompds.Services;
using Microsoft.AspNetCore.Mvc;
using PendingAccounts.Services;

namespace atompds.Endpoints.Pending;

public static class PendingEmailEndpoints
{
    public static RouteGroupBuilder MapPendingEmailEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/requestEmailConfirmation", RequestEmailConfirmationAsync).WithMetadata(new PendingAccessAttribute());
        group.MapPost("/confirmEmail", ConfirmEmailAsync).WithMetadata(new PendingAccessAttribute());
        group.MapPost("/requestEmailUpdate", RequestEmailUpdateAsync).WithMetadata(new PendingAccessAttribute());
        group.MapPost("/updateEmail", UpdateEmailAsync).WithMetadata(new PendingAccessAttribute());
        return group;
    }

    private static async Task<IResult> RequestEmailConfirmationAsync(
        HttpContext context,
        PendingAccountService pendingAccountService,
        BackgroundEmailDispatcher mailer)
    {
        var pendingId = PendingHelper.GetPendingRegistrationId(context);
        if (pendingId == null) return Results.Unauthorized();

        var profile = await pendingAccountService.GetProfileAsync(pendingId.Value);
        if (profile == null) return Results.NotFound();

        var token = await pendingAccountService.RequestEmailConfirmationAsync(pendingId.Value);
        if (token == null)
            return Results.BadRequest(new { error = "Unable to send confirmation email" });

        await mailer.SendEmailConfirmationAsync(token, profile.Email);
        return Results.Ok();
    }

    private static async Task<IResult> ConfirmEmailAsync(
        HttpContext context,
        [FromBody] PendingConfirmEmailRequest request,
        PendingAccountService pendingAccountService)
    {
        var pendingId = PendingHelper.GetPendingRegistrationId(context);
        if (pendingId == null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest(new { error = "Token is required" });

        var success = await pendingAccountService.ConfirmEmailAsync(pendingId.Value, request.Token);
        if (!success)
            return Results.BadRequest(new { error = "Invalid or expired token" });

        return Results.Ok();
    }

    private static async Task<IResult> RequestEmailUpdateAsync(
        HttpContext context,
        PendingAccountService pendingAccountService,
        BackgroundEmailDispatcher mailer)
    {
        var pendingId = PendingHelper.GetPendingRegistrationId(context);
        if (pendingId == null) return Results.Unauthorized();

        var profile = await pendingAccountService.GetProfileAsync(pendingId.Value);
        if (profile == null) return Results.NotFound();

        var token = await pendingAccountService.RequestEmailUpdateAsync(pendingId.Value);
        if (token == null)
            return Results.BadRequest(new { error = "Unable to send verification email" });

        await mailer.SendEmailUpdateAsync(token, profile.Email);
        return Results.Ok();
    }

    private static async Task<IResult> UpdateEmailAsync(
        HttpContext context,
        [FromBody] PendingUpdateEmailRequest request,
        PendingAccountService pendingAccountService,
        EmailAddressValidator emailAddressValidator)
    {
        var pendingId = PendingHelper.GetPendingRegistrationId(context);
        if (pendingId == null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest(new { error = "Email is required" });

        await emailAddressValidator.AssertSupportedEmailAsync(request.Email);

        var success = await pendingAccountService.UpdateEmailAsync(pendingId.Value, request.Email);
        if (!success)
            return Results.BadRequest(new { error = "Email already in use" });

        return Results.Ok();
    }
}
