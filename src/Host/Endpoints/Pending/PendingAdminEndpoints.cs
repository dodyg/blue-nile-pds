using BlueNilePds.Host.Configuration;
using BlueNilePds.Host.Endpoints.Pending.Models;
using BlueNilePds.Host.Middleware;
using BlueNilePds.Host.Services;
using Microsoft.AspNetCore.Mvc;
using BlueNilePds.Pds.PendingAccounts.Models;
using BlueNilePds.Pds.PendingAccounts.Services;

namespace BlueNilePds.Host.Endpoints.Pending;

public static class PendingAdminEndpoints
{
    public static RouteGroupBuilder MapPendingAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/pending/list", ListPendingAsync).WithMetadata(new AdminTokenAttribute());
        group.MapGet("/pending/{id:int}", GetPendingDetailAsync).WithMetadata(new AdminTokenAttribute());
        group.MapPost("/pending/approve", ApproveAsync).WithMetadata(new AdminTokenAttribute());
        group.MapPost("/pending/reject", RejectAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> GetPendingDetailAsync(
        int id,
        PendingAccountService pendingAccountService)
    {
        var result = await pendingAccountService.GetAdminDetailViewAsync(id);
        if (result == null) return Results.NotFound();
        return Results.Ok(result);
    }

    private static async Task<IResult> ListPendingAsync(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        PendingAccountService? pendingAccountService = null)
    {
        var result = await pendingAccountService!.ListPendingAsync(cursor, limit);
        return Results.Ok(result);
    }

    private static async Task<IResult> ApproveAsync(
        [FromBody] PendingApproveRequest request,
        PendingAccountService pendingAccountService,
        PendingApprovalService pendingApprovalService,
        BackgroundEmailDispatcher mailer,
        ServerEnvironment environment)
    {
        var pendingId = request.Id;

        var registration = await pendingAccountService.GetRegistrationAsync(pendingId);
        if (registration == null)
            return Results.BadRequest(new { error = "Pending registration not found" });

        if (registration.Status != PendingRegistrationStatus.Pending)
            return Results.BadRequest(new { error = "Registration already processed" });

        var profile = await pendingAccountService.GetProfileAsync(pendingId);

        try
        {
            var result = await pendingApprovalService.CreatePdsAccountAsync(
                registration.Handle,
                registration.Email,
                registration.PasswordScrypt,
                registration.InviteCode,
                profile?.Location,
                profile?.AccountType);

            await pendingAccountService.ApproveAsync(pendingId);

            if (!string.IsNullOrWhiteSpace(registration.Email))
            {
                await mailer.SendCustomEmailAsync(
                    "Your account has been approved",
                    $"Hello {registration.Handle},\n\nYour account has been approved and is now active.\n\nBest,\n{environment.PDS_SERVICE_NAME}",
                    registration.Email);
            }

            return Results.Ok(new { did = result.Did, handle = result.Handle });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to create PDS account: {ex.Message}" });
        }
    }

    private static async Task<IResult> RejectAsync(
        [FromBody] PendingRejectRequest request,
        PendingAccountService pendingAccountService,
        BackgroundEmailDispatcher mailer,
        ServerEnvironment environment)
    {
        var pendingId = request.Id;

        var registration = await pendingAccountService.GetRegistrationAsync(pendingId);
        if (registration == null)
            return Results.BadRequest(new { error = "Pending registration not found" });

        if (registration.Status != PendingRegistrationStatus.Pending)
            return Results.BadRequest(new { error = "Registration already processed" });

        if (!string.IsNullOrWhiteSpace(registration.Email))
        {
            await mailer.SendCustomEmailAsync(
                "Your account was not approved",
                $"Hello {registration.Handle},\n\nUnfortunately your account was not approved. If you believe this is a mistake, please contact support.\n\nBest,\n{environment.PDS_SERVICE_NAME}",
                registration.Email);
        }

        await pendingAccountService.RejectAsync(pendingId, request.Reason);

        return Results.Ok();
    }
}
