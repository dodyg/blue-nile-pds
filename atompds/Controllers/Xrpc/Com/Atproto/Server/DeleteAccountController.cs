using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Middleware;
using DidLib;
using Microsoft.AspNetCore.Mvc;
using Sequencer;
using Xrpc;
using FishyFlip.Lexicon.Com.Atproto.Server;
using Mailer;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class DeleteAccountController : ControllerBase
{
    private readonly ILogger<DeleteAccountController> _logger;
    private readonly AccountRepository _accountRepository;
    private readonly ActorRepository _actorRepository;
    private readonly SequencerRepository _sequencer;
    private readonly IMailer _mailer;
    private readonly PlcClient _plcClient;
    public DeleteAccountController(ILogger<DeleteAccountController> logger,
        AccountRepository accountRepository,
        ActorRepository actorRepository,
        SequencerRepository sequencer,
        IMailer mailer,
        PlcClient plcClient)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _actorRepository = actorRepository;
        _sequencer = sequencer;
        _mailer = mailer;
        _plcClient = plcClient;

    }
    
    [HttpPost("com.atproto.server.requestAccountDelete")]
    [AccessFull(checkTakenDown:true)]
    public async Task RequestAccountDelete(CancellationToken cancellationToken)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;
        var account = await _accountRepository.GetAccount(did, new AvailabilityFlags(IncludeTakenDown: true, IncludeDeactivated: true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found."));
        }

        if (account.Email == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Account has no email."));
        }

        var emailToken = await _accountRepository.CreateEmailToken(did, EmailToken.EmailTokenPurpose.delete_account);
        await _mailer.SendAccountDelete(emailToken, account.Email);
    }
    
    [HttpPost("com.atproto.server.deleteAccount")]
    public async Task DeleteAccount(
        [FromBody] DeleteAccountInput input,
        CancellationToken cancellationToken)
    {
        var did = input.Did?.Handler;
        if (did == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("InvalidDid", "Invalid DID."));
        }
        
        var account = await _accountRepository.GetAccount(did, new AvailabilityFlags(IncludeTakenDown: true, IncludeDeactivated: true));
        if (account == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("AccountNotFound", "Account not found."));
        }
        
        var validPass = await _accountRepository.VerifyAccountPassword(did, input.Password!);
        if (!validPass)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Invalid did or password"));
        }
        
        await _accountRepository.AssertValidEmailToken(did, input.Token!, EmailToken.EmailTokenPurpose.delete_account);
        await _accountRepository.DeleteAccount(did);
        var accountSeq = await _sequencer.SequenceAccountEvent(did, AccountStore.AccountStatus.Deleted);
        var tombstoneSeq = await _sequencer.SequenceTombstoneEvent(did);
        await _sequencer.DeleteAllForUser(did, [accountSeq, tombstoneSeq]);
    }
}