using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Services;
using atompds.Utils;
using CarpaNet;
using CommonWeb;
using ComAtproto.Server;
using Config;
using Crypto.Secp256k1;
using DidLib;
using Handle;
using Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
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
    private readonly CaptchaVerifier _captchaVerifier;
    private readonly HandleManager _handle;
    private readonly IdentityConfig _identityConfig;
    private readonly IdResolver _idResolver;
    private readonly InvitesConfig _invitesConfig;
    private readonly ILogger<CreateAccountController> _logger;
    private readonly PlcClient _plcClient;
    private readonly SecretsConfig _secretsConfig;
    private readonly SequencerRepository _sequencer;
    private readonly ServiceConfig _serviceConfig;
    private readonly EmailAddressValidator _emailAddressValidator;

    public CreateAccountController(ILogger<CreateAccountController> logger,
        AccountRepository accountRepository,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        InvitesConfig invitesConfig,
        HandleManager handle,
        ActorRepositoryProvider actorRepositoryProvider,
        IdResolver idResolver,
        SecretsConfig secretsConfig,
        SequencerRepository sequencer,
        PlcClient plcClient,
        CaptchaVerifier captchaVerifier,
        EmailAddressValidator emailAddressValidator)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _invitesConfig = invitesConfig;
        _handle = handle;
        _actorRepositoryProvider = actorRepositoryProvider;
        _idResolver = idResolver;
        _secretsConfig = secretsConfig;
        _sequencer = sequencer;
        _plcClient = plcClient;
        _captchaVerifier = captchaVerifier;
        _emailAddressValidator = emailAddressValidator;
    }


    // TODO: Optional auth used to validate DID transfer
    [HttpPost("com.atproto.server.createAccount")]
    public async Task<IActionResult> CreateAccountAsync([FromBody] CreateAccountInput request)
    {
        string? validatedDid = null;
        SqliteConnection? conn = null;
        try
        {
            await _captchaVerifier.VerifyAsync(request.VerificationCode);

            var validatedInputs = await ValidateInputsForLocalPdsAsync(request);
            validatedDid = validatedInputs.did;

            await using var actorStoreDb = _actorRepositoryProvider.Create(validatedInputs.did, validatedInputs.signingKey);
            conn = actorStoreDb.Connection;
            var commit = await actorStoreDb.TransactRepoAsync(async repo =>
            {
                var commit = await repo.Repo.CreateRepoAsync([]);
                return commit;
            });

            if (validatedInputs.plcOp != null)
            {
                try
                {
                    await _plcClient.SendOperationAsync(validatedDid, validatedInputs.plcOp);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to create did:plc for {didKey}, {handle}", validatedInputs.did, validatedInputs.handle);
                    throw new XRPCError(new InvalidRequestErrorDetail("Failed to create did:plc"), e);
                }
            }

            var didDoc = await SafeResolveDidDocAsync(validatedInputs.did, true);
            var creds = await _accountRepository.CreateAccountAsync(validatedInputs.did,
                validatedInputs.handle,
                validatedInputs.Email,
                validatedInputs.Password,
                commit.Cid.ToString(),
                commit.Rev,
                validatedInputs.InviteCode,
                validatedInputs.deactivated);

            if (!validatedInputs.deactivated)
            {
                await _sequencer.SequenceIdentityEventAsync(validatedInputs.did, validatedInputs.handle);
                await _sequencer.SequenceAccountEventAsync(validatedInputs.did, AccountStore.AccountStatus.Active);
                await _sequencer.SequenceCommitAsync(validatedInputs.did, commit, []);
            }

            await _accountRepository.UpdateRepoRootAsync(validatedInputs.did, commit.Cid, commit.Rev);
            // TODO: clear reserved keypair

            return Ok(new CreateAccountOutput
            {
                Did = new ATDid(validatedInputs.did),
                AccessJwt = creds.AccessJwt,
                RefreshJwt = creds.RefreshJwt,
                DidDoc = didDoc?.ToJsonElement(),
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

    private async Task<DidDocument?> SafeResolveDidDocAsync(string did, bool forceRefresh = false)
    {
        try
        {
            var didDoc = await _idResolver.DidResolver.ResolveAsync(did, forceRefresh);
            return didDoc;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve did doc: {did}", did);
            return null;
        }
    }

    private async Task<(string did, string handle, string Email, string? Password, string? InviteCode, Secp256k1Keypair signingKey, SignedOp<AtProtoOp>? plcOp, bool
        deactivated)> ValidateInputsForLocalPdsAsync(CreateAccountInput createAccountInput)
    {
        if (_invitesConfig.Required && string.IsNullOrWhiteSpace(createAccountInput.InviteCode))
        {
            throw new XRPCError(new InvalidInviteCodeErrorDetail("No invite code provided"));
        }

        if (createAccountInput.Email == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Email is required"));
        }
        await _emailAddressValidator.AssertSupportedEmailAsync(createAccountInput.Email);

        var signingKey = Secp256k1Keypair.Create(true);

        if (createAccountInput.Did != null)
        {
            var did = createAccountInput.Did.Value;
            var handle = await _handle.NormalizeAndValidateHandleAsync(createAccountInput.Handle.Value, did, false);

            if (_invitesConfig.Required && createAccountInput.InviteCode != null)
            {
                await _accountRepository.EnsureInviteIsAvailableAsync(createAccountInput.InviteCode);
            }

            var existingAccount = await _accountRepository.GetAccountAsync(did);
            if (existingAccount != null)
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Account already exists"));
            }

            SignedOp<AtProtoOp>? plcOp = null;
            if (createAccountInput.PlcOp != null)
            {
                plcOp = DeserializePlcOp(createAccountInput.PlcOp.Value);
            }

            return (did, handle, createAccountInput.Email, createAccountInput.Password, createAccountInput.InviteCode, signingKey, plcOp, false);
        }

        var validatedHandle = await _handle.NormalizeAndValidateHandleAsync(createAccountInput.Handle.Value, createAccountInput.Did?.Value, false);

        if (_invitesConfig.Required && createAccountInput.InviteCode != null)
        {
            await _accountRepository.EnsureInviteIsAvailableAsync(createAccountInput.InviteCode);
        }

        var handleAcct = await _accountRepository.GetAccountAsync(validatedHandle);
        var emailAcct = await _accountRepository.GetAccountAsync(createAccountInput.Email);
        if (handleAcct != null)
        {
            throw new XRPCError(new HandleNotAvailableErrorDetail($"Handle already taken: {validatedHandle}"));
        }
        if (emailAcct != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Email already taken: {createAccountInput.Email}"));
        }

        var (did2, plcOp2) = await FormatDidAndPlcOpAsync(validatedHandle, createAccountInput, signingKey);

        return (did2, validatedHandle, createAccountInput.Email, createAccountInput.Password, createAccountInput.InviteCode, signingKey, plcOp2, false);
    }

    private SignedOp<AtProtoOp> DeserializePlcOp(JsonElement plcOpJson)
    {
        var opStr = plcOpJson.GetRawText();
        var cborOp = PeterO.Cbor.CBORObject.FromJSONString(opStr);
        var sig = cborOp.ContainsKey("sig") ? cborOp["sig"].AsString() : "";
        var op = AtProtoOp.FromCborObject(cborOp);
        return new SignedOp<AtProtoOp> { Op = op, Sig = sig };
    }

    private async Task<(string Did, SignedOp<AtProtoOp> PlcOp)> FormatDidAndPlcOpAsync(string handle, CreateAccountInput createAccountInput, Secp256k1Keypair signingKey)
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

        var plcCreate = await Operations.CreateOpAsync(signingKey.Did(), handle, _serviceConfig.PublicUrl, rotationKeys, _secretsConfig.PlcRotationKey);
        return (plcCreate.Did, plcCreate.Op);
    }

}
