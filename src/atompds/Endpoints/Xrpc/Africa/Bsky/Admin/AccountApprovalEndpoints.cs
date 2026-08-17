using AccountManager;
using AccountManager.Db;
using AfricaBsky.Admin;
using atompds.Config;
using atompds.Middleware;
using atompds.Services;
using CarpaNet;
using Config;
using Sequencer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Africa.Bsky.Admin;

public static class AccountApprovalEndpoints
{
    public static RouteGroupBuilder MapAccountApprovalEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("africa.bsky.admin.listPendingAccounts", ListPendingAccountsAsync)
            .WithMetadata(new AdminTokenAttribute());
        group.MapPost("africa.bsky.admin.approveAccount", ApproveAccountAsync)
            .WithMetadata(new AdminTokenAttribute());
        group.MapPost("africa.bsky.admin.rejectAccount", RejectAccountAsync)
            .WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> ListPendingAccountsAsync(
        string? cursor,
        long? limit,
        AccountRepository accountRepository)
    {
        var (accounts, nextCursor) = await accountRepository.GetPendingAccountsAsync(cursor, (int)(limit ?? 50));

        return Results.Ok(new ListPendingAccountsOutput
        {
            Accounts = accounts.Select(MapPendingAccountView).ToList(),
            Cursor = nextCursor
        });
    }

    private static DefsPendingAccountView MapPendingAccountView(ActorAccount account)
    {
        return new DefsPendingAccountView
        {
            Did = new ATDid(account.Did),
            Handle = new ATHandle(account.Handle ?? account.Did),
            Email = account.Email ?? string.Empty,
            Location = account.Location,
            AccountType = account.AccountType,
            CreatedAt = new DateTimeOffset(account.CreatedAt, TimeSpan.Zero),
            EmailConfirmed = account.EmailConfirmedAt != null
        };
    }

    private static async Task<IResult> ApproveAccountAsync(
        ApproveAccountInput request,
        AccountRepository accountRepository,
        SequencerRepository sequencer,
        BackgroundEmailDispatcher mailer,
        ServerEnvironment environment)
    {
        var did = (string)request.Did;
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("AccountNotFound", "Account not found"));

        await accountRepository.UnsuspendAccountAsync(did);
        await sequencer.SequenceAccountEventAsync(did, AccountStore.AccountStatus.Active);

        var userEmail = account.Email;
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            await mailer.SendCustomEmailAsync(
                "Your account has been approved",
                $"Hello {account.Handle},\n\nYour account has been approved and is now active.\n\nBest,\n{environment.PDS_SERVICE_NAME}",
                userEmail);
        }

        return Results.Ok(new ApproveAccountOutput { Handle = account.Handle != null ? new ATHandle(account.Handle) : null });
    }

    private static async Task<IResult> RejectAccountAsync(
        RejectAccountInput request,
        AccountRepository accountRepository,
        BackgroundEmailDispatcher mailer,
        ServerEnvironment environment)
    {
        var did = (string)request.Did;
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("AccountNotFound", "Account not found"));

        var userEmail = account.Email;
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            await mailer.SendCustomEmailAsync(
                "Your account was not approved",
                $"Hello {account.Handle},\n\nUnfortunately your account was not approved. If you believe this is a mistake, please contact support.\n\nBest,\n{environment.PDS_SERVICE_NAME}",
                userEmail);
        }

        return Results.Ok();
    }
}
