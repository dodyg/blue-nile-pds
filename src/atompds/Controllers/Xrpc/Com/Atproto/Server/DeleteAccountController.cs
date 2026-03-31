using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using ComAtproto.Server;
using Mailer;
using Microsoft.AspNetCore.Mvc;
using Sequencer;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class DeleteAccountController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ILogger<DeleteAccountController> _logger;
    private readonly IMailer _mailer;
    private readonly SequencerRepository _sequencer;
    public DeleteAccountController(ILogger<DeleteAccountController> logger,
        AccountRepository accountRepository,
        SequencerRepository sequencer,
        IMailer mailer)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _sequencer = sequencer;
        _mailer = mailer;

    }

    [HttpPost("com.atproto.server.requestAccountDelete")]
    [AccessFull(true)]
    public async Task RequestAccountDeleteAsync(CancellationToken cancellationToken)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found."));
        }

        if (account.Email == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account has no email."));
        }

        var emailToken = await _accountRepository.CreateEmailTokenAsync(did, EmailToken.EmailTokenPurpose.delete_account);
        await _mailer.SendAccountDeleteAsync(emailToken, account.Email);
    }

    [HttpPost("com.atproto.server.deleteAccount")]
    public async Task DeleteAccountAsync(
        [FromBody] DeleteAccountInput input,
        CancellationToken cancellationToken)
    {
        var did = input.Did.Value;
        if (string.IsNullOrWhiteSpace(did))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("InvalidDid", "Invalid DID."));
        }

        var account = await _accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("AccountNotFound", "Account not found."));
        }

        var validPass = await _accountRepository.VerifyAccountPasswordAsync(did, input.Password!);
        if (!validPass)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Invalid did or password"));
        }

        await _accountRepository.AssertValidEmailTokenAsync(did, input.Token!, EmailToken.EmailTokenPurpose.delete_account);
        await _accountRepository.DeleteAccountAsync(did);
        var accountSeq = await _sequencer.SequenceAccountEventAsync(did, AccountStore.AccountStatus.Deleted);
        var tombstoneSeq = await _sequencer.SequenceTombstoneEventAsync(did);
        await _sequencer.DeleteAllForUserAsync(did, [accountSeq, tombstoneSeq]);
    }
}
