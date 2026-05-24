using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Services;
using ComAtproto.Server;
using Sequencer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class DeleteAccountEndpoints
{
    public static RouteGroupBuilder MapDeleteAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.requestAccountDelete", RequestDeleteAsync).WithMetadata(new AccessFullAttribute(true));
        group.MapPost("com.atproto.server.deleteAccount", DeleteAsync);
        return group;
    }

    private static async Task RequestDeleteAsync(
        HttpContext context,
        AccountRepository accountRepository,
        BackgroundEmailDispatcher mailer,
        CancellationToken cancellationToken)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found."));

        if (account.Email == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account has no email."));

        var emailToken = await accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.delete_account);
        await mailer.SendAccountDeleteAsync(emailToken, account.Email);
    }

    private static async Task<IResult> DeleteAsync(
        DeleteAccountInput input,
        AccountRepository accountRepository,
        SequencerRepository sequencer,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var did = input.Did.Value;
        if (string.IsNullOrWhiteSpace(did))
            throw new XRPCError(new InvalidRequestErrorDetail("InvalidDid", "Invalid DID."));

        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("AccountNotFound", "Account not found."));

        var validPass = await accountRepository.VerifyAccountPasswordAsync(did, input.Password!);
        if (!validPass)
            throw new XRPCError(new AuthRequiredErrorDetail("Invalid did or password"));

        await accountRepository.AssertValidEmailTokenAsync(did, input.Token!, EmailToken.EmailTokenPurpose.delete_account);
        await accountRepository.DeleteAccountAsync(did);
        var logger = loggerFactory.CreateLogger("DeleteAccountEndpoints");
        logger.LogWarning("Account deleted: {Did}", did);
        var accountSeq = await sequencer.SequenceAccountEventAsync(did, AccountStore.AccountStatus.Deleted);
        var tombstoneSeq = await sequencer.SequenceTombstoneEventAsync(did);
        await sequencer.DeleteAllForUserAsync(did, [accountSeq, tombstoneSeq]);

        return Results.Ok();
    }
}
