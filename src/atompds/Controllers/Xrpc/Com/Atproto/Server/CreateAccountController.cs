using System.Net.Mail;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Payloads.Server;
using atompds.Utils;
using CommonWeb;
using Config;
using Crypto.Secp256k1;
using DidLib;
using FishyFlip.Lexicon.Com.Atproto.Server;
using FishyFlip.Models;
using Handle;
using Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Sequencer;
using Xrpc;
using Operations = DidLib.Operations;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class CreateAccountController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly HandleManager _handle;
    private readonly HttpClient _httpClient;
    private readonly IdentityConfig _identityConfig;
    private readonly IdResolver _idResolver;
    private readonly InvitesConfig _invitesConfig;
    private readonly ILogger<CreateAccountController> _logger;
    private readonly PlcClient _plcClient;
    private readonly SecretsConfig _secretsConfig;
    private readonly SequencerRepository _sequencer;
    private readonly ServiceConfig _serviceConfig;

    public CreateAccountController(ILogger<CreateAccountController> logger,
        AccountRepository accountRepository,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        InvitesConfig invitesConfig,
        HttpClient httpClient,
        HandleManager handle,
        ActorRepositoryProvider actorRepositoryProvider,
        IdResolver idResolver,
        SecretsConfig secretsConfig,
        SequencerRepository sequencer,
        PlcClient plcClient)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _invitesConfig = invitesConfig;
        _httpClient = httpClient;
        _handle = handle;
        _actorRepositoryProvider = actorRepositoryProvider;
        _idResolver = idResolver;
        _secretsConfig = secretsConfig;
        _sequencer = sequencer;
        _plcClient = plcClient;
    }


    // TODO: Optional auth used to validate DID transfer
    [HttpPost("com.atproto.server.createAccount")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        string? validatedDid = null;
        SqliteConnection? conn = null;
        try
        {
            var validatedInputs = await ValidateInputsForLocalPds(request);
            validatedDid = validatedInputs.did;

            await using var actorStoreDb = _actorRepositoryProvider.Create(validatedInputs.did, validatedInputs.signingKey);
            conn = actorStoreDb.Connection;
            var commit = await actorStoreDb.TransactRepo(async repo =>
            {
                var commit = await repo.Repo.CreateRepo([]);
                return commit;
            });

            if (validatedInputs.plcOp != null)
            {
                try
                {
                    await _plcClient.SendOperation(validatedDid, validatedInputs.plcOp);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to create did:plc for {didKey}, {handle}", validatedInputs.did, validatedInputs.handle);
                    throw new XRPCError(new InvalidRequestErrorDetail("Failed to create did:plc"), e);
                }
            }

            var didDoc = await SafeResolveDidDoc(validatedInputs.did, true);
            var creds = await _accountRepository.CreateAccount(validatedInputs.did,
                validatedInputs.handle,
                validatedInputs.Email,
                validatedInputs.Password,
                commit.Cid.ToString(),
                commit.Rev,
                validatedInputs.InviteCode,
                validatedInputs.deactivated);

            if (!validatedInputs.deactivated)
            {
                await _sequencer.SequenceIdentityEvent(validatedInputs.did, validatedInputs.handle);
                await _sequencer.SequenceAccountEvent(validatedInputs.did, AccountStore.AccountStatus.Active);
                await _sequencer.SequenceCommit(validatedInputs.did, commit, []);
            }

            await _accountRepository.UpdateRepoRoot(validatedInputs.did, commit.Cid, commit.Rev);
            // TODO: clear reserved keypair

            return Ok(new CreateAccountOutput
            {
                Did = new ATDid(validatedInputs.did),
                AccessJwt = creds.AccessJwt,
                RefreshJwt = creds.RefreshJwt,
                DidDoc = didDoc?.ToDidDoc(),
                Handle = new ATHandle(validatedInputs.handle)
            });
        }
        catch (Exception e)
        {
            // if exception, delete actorstore
            _logger.LogError(e, "Failed to create account");
            if (!string.IsNullOrWhiteSpace(validatedDid))
            {
                if (conn != null)
                {
                    SqliteConnection.ClearPool(conn);
                }
                _actorRepositoryProvider.Destroy(validatedDid);
            }
            throw;
        }
    }

    private async Task<DidDocument?> SafeResolveDidDoc(string did, bool forceRefresh = false)
    {
        try
        {
            var didDoc = await _idResolver.DidResolver.Resolve(did, forceRefresh);
            return didDoc;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve did doc: {did}", did);
            return null;
        }
    }

    private async Task<(string did, string handle, string Email, string? Password, string? InviteCode, Secp256k1Keypair signingKey, SignedOp<AtProtoOp>? plcOp, bool
        deactivated)> ValidateInputsForLocalPds(CreateAccountRequest createAccountInput)
    {
        // if (createAccountInput.PlcOp != null)
        // {
        //     throw new XRPCError(new InvalidRequestErrorDetail("Unsupported input: \"plcOp\""));
        // }

        if (_invitesConfig.Required && string.IsNullOrWhiteSpace(createAccountInput.InviteCode))
        {
            throw new XRPCError(new InvalidInviteCodeErrorDetail("No invite code provided"));
        }

        if (createAccountInput.Email == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Email is required"));
        }
        if (!IsValidEmail(createAccountInput.Email) || await IsDisposableEmail(createAccountInput.Email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("This email address is not supported, please use a different email."));
        }

        var handle = await _handle.NormalizeAndValidateHandle(createAccountInput.Handle, createAccountInput.Did, false);

        if (_invitesConfig.Required && createAccountInput.InviteCode != null)
        {
            await _accountRepository.EnsureInviteIsAvailable(createAccountInput.InviteCode);
        }

        var handleAcct = await _accountRepository.GetAccount(handle);
        var emailAcct = await _accountRepository.GetAccount(createAccountInput.Email);
        if (handleAcct != null)
        {
            throw new XRPCError(new HandleNotAvailableErrorDetail($"Handle already taken: {handle}"));
        }
        if (emailAcct != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Email already taken: {createAccountInput.Email}"));
        }

        var signingKey = Secp256k1Keypair.Create(true);

        string did;
        SignedOp<AtProtoOp> plcOp;
        var deactivated = false;
        if (createAccountInput.Did != null)
        {
            // if did != requested, throw error
            deactivated = true;
            throw new XRPCError(new InvalidRequestErrorDetail("This PDS does not support DID transfer"));
        }
        (did, plcOp) = await FormatDidAndPlcOp(handle, createAccountInput, signingKey);

        return (did, handle, createAccountInput.Email, createAccountInput.Password, createAccountInput.InviteCode, signingKey, plcOp, deactivated);
    }

    private async Task<(string Did, SignedOp<AtProtoOp> PlcOp)> FormatDidAndPlcOp(string handle, CreateAccountRequest createAccountInput, Secp256k1Keypair signingKey)
    {
        string[] rotationKeys = [_secretsConfig.PlcRotationKey.Did()];
        if (_identityConfig.RecoveryDidKey != null)
        {
            rotationKeys = [_identityConfig.RecoveryDidKey, ..rotationKeys];
        }
        if (createAccountInput.RecoveryKey != null)
        {
            rotationKeys = [createAccountInput.RecoveryKey, ..rotationKeys];
        }

        var plcCreate = await Operations.CreateOp(signingKey.Did(), handle, _serviceConfig.PublicUrl, rotationKeys, _secretsConfig.PlcRotationKey);
        return (plcCreate.Did, plcCreate.Op);
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
    private async Task<bool> IsDisposableEmail(string email)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://open.kickbox.com/v1/disposable/{email}");
            var content = await response.Content.ReadAsStringAsync();
            var disposableResponse = JsonConvert.DeserializeObject<DisposableResponse>(content);
            if (disposableResponse == null)
            {
                return false;
            }

            return disposableResponse.Disposable;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to check if email is disposable");
            return false;
        }
    }

    private record DisposableResponse([property: JsonProperty("disposable")] bool Disposable);
}