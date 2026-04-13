using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using atompds.Middleware;
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
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly AuthVerifier _authVerifier;
    private readonly CaptchaVerifier _captchaVerifier;
    private readonly EmailAddressValidator _emailAddressValidator;
    private readonly EntrywayRelayService _entrywayRelayService;
    private readonly HandleManager _handle;
    private readonly IdentityConfig _identityConfig;
    private readonly IdResolver _idResolver;
    private readonly InvitesConfig _invitesConfig;
    private readonly ILogger<CreateAccountController> _logger;
    private readonly PlcClient _plcClient;
    private readonly ReservedSigningKeyStore _reservedSigningKeyStore;
    private readonly SecretsConfig _secretsConfig;
    private readonly SequencerRepository _sequencer;
    private readonly ServiceConfig _serviceConfig;

    public CreateAccountController(
        ILogger<CreateAccountController> logger,
        AccountRepository accountRepository,
        AuthVerifier authVerifier,
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
        ReservedSigningKeyStore reservedSigningKeyStore,
        EmailAddressValidator emailAddressValidator,
        EntrywayRelayService entrywayRelayService)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _authVerifier = authVerifier;
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
        _reservedSigningKeyStore = reservedSigningKeyStore;
        _emailAddressValidator = emailAddressValidator;
        _entrywayRelayService = entrywayRelayService;
    }

    [HttpPost("com.atproto.server.createAccount")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> CreateAccountAsync([FromBody] CreateAccountInput request)
    {
        string? validatedDid = null;
        SqliteConnection? conn = null;
        try
        {
            if (!_entrywayRelayService.IsConfigured)
            {
                await _captchaVerifier.VerifyAsync(request.VerificationCode);
            }

            var validatedInputs = _entrywayRelayService.IsConfigured
                ? await ValidateInputsForEntrywayPdsAsync(request)
                : await ValidateInputsForLocalPdsAsync(request);
            validatedDid = validatedInputs.Did;

            await using var actorStoreDb = _actorRepositoryProvider.Create(validatedInputs.Did, validatedInputs.SigningKey);
            conn = actorStoreDb.Connection;
            var commit = await actorStoreDb.TransactRepoAsync(async repo => await repo.Repo.CreateRepoAsync([]));

            if (validatedInputs.PlcOp != null)
            {
                try
                {
                    await _plcClient.SendOperationAsync(validatedInputs.Did, validatedInputs.PlcOp);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to create did:plc for {didKey}, {handle}", validatedInputs.Did, validatedInputs.Handle);
                    throw new XRPCError(new InvalidRequestErrorDetail("Failed to create did:plc"), e);
                }
            }

            var didDoc = await SafeResolveDidDocAsync(validatedInputs.Did, true);
            var creds = await _accountRepository.CreateAccountAsync(
                validatedInputs.Did,
                validatedInputs.Handle,
                validatedInputs.Email,
                validatedInputs.Password,
                commit.Cid.ToString(),
                commit.Rev,
                validatedInputs.InviteCode,
                validatedInputs.Deactivated);

            if (!validatedInputs.Deactivated)
            {
                await _sequencer.SequenceIdentityEventAsync(validatedInputs.Did, validatedInputs.Handle);
                await _sequencer.SequenceAccountEventAsync(validatedInputs.Did, AccountStore.AccountStatus.Active);
                await _sequencer.SequenceCommitAsync(validatedInputs.Did, commit, []);
            }

            await _accountRepository.UpdateRepoRootAsync(validatedInputs.Did, commit.Cid, commit.Rev);

            return Ok(new CreateAccountOutput
            {
                Did = new ATDid(validatedInputs.Did),
                AccessJwt = creds.AccessJwt,
                RefreshJwt = creds.RefreshJwt,
                DidDoc = didDoc?.ToJsonElement(),
                Handle = new ATHandle(validatedInputs.Handle)
            });
        }
        catch (Exception e)
        {
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
            return await _idResolver.DidResolver.ResolveAsync(did, forceRefresh);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve did doc: {did}", did);
            return null;
        }
    }

    private async Task<ValidatedCreateAccount> ValidateInputsForEntrywayPdsAsync(CreateAccountInput createAccountInput)
    {
        if (createAccountInput.Did == null || createAccountInput.PlcOp == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Entryway account creation requires \"did\" and \"plcOp\""));
        }

        var did = createAccountInput.Did.Value;
        var plcOp = JsonSerializer.Deserialize<SignedOp<AtProtoOp>>(createAccountInput.PlcOp.Value.GetRawText())
            ?? throw new XRPCError(new IncompatibleDidDocErrorDetail("invalid plc operation"));
        var handle = await _handle.NormalizeAndValidateHandleAsync(createAccountInput.Handle.Value, did, false);

        if (!string.Equals(plcOp.Op.Type, "plc_operation", StringComparison.Ordinal) || plcOp.Op.Prev != null)
        {
            throw new XRPCError(new IncompatibleDidDocErrorDetail("invalid plc operation"));
        }

        var derivedDid = await Operations.DidForCreateOpAsync(plcOp);
        if (!string.Equals(derivedDid, did, StringComparison.Ordinal))
        {
            throw new XRPCError(new IncompatibleDidDocErrorDetail("provided DID does not match plcOp"));
        }

        if (!plcOp.Op.VerificationMethods.TryGetValue("atproto", out var signingDid) || string.IsNullOrWhiteSpace(signingDid))
        {
            throw new XRPCError(new IncompatibleDidDocErrorDetail("provided DID document is missing an atproto signing key"));
        }

        var reservedSigningKey = await _reservedSigningKeyStore.ConsumeAsync(did)
            ?? await _reservedSigningKeyStore.ConsumeAsync(signingDid);
        if (reservedSigningKey == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Reserved signing key does not exist"));
        }

        if (!string.Equals(reservedSigningKey.Did(), signingDid, StringComparison.Ordinal))
        {
            throw new XRPCError(new IncompatibleDidDocErrorDetail("DID document signing key does not match reserved signing key"));
        }

        if (!plcOp.Op.AlsoKnownAs.Contains($"at://{handle}", StringComparer.Ordinal))
        {
            throw new XRPCError(new IncompatibleDidDocErrorDetail("provided handle does not match DID document handle"));
        }

        if (!plcOp.Op.Services.TryGetValue("atproto_pds", out var pdsService) ||
            !string.Equals(pdsService.Type, "AtprotoPersonalDataServer", StringComparison.Ordinal) ||
            !string.Equals(NormalizeUrl(pdsService.Endpoint), NormalizeUrl(_serviceConfig.PublicUrl), StringComparison.Ordinal))
        {
            throw new XRPCError(new IncompatibleDidDocErrorDetail("DID document pds endpoint does not match service endpoint"));
        }

        return new ValidatedCreateAccount(
            did,
            handle,
            null,
            null,
            null,
            reservedSigningKey,
            plcOp,
            false);
    }

    private async Task<ValidatedCreateAccount> ValidateInputsForLocalPdsAsync(CreateAccountInput createAccountInput)
    {
        if (createAccountInput.PlcOp != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Unsupported input: \"plcOp\""));
        }

        if (!string.IsNullOrWhiteSpace(createAccountInput.VerificationPhone))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Unsupported input: \"verificationPhone\""));
        }

        if (_invitesConfig.Required && string.IsNullOrWhiteSpace(createAccountInput.InviteCode))
        {
            throw new XRPCError(new InvalidInviteCodeErrorDetail("No invite code provided"));
        }

        if (createAccountInput.Email == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Email is required"));
        }

        await _emailAddressValidator.AssertSupportedEmailAsync(createAccountInput.Email);

        if (createAccountInput.Did != null)
        {
            var did = createAccountInput.Did.Value;
            var requesterDid = await GetRequesterDidAsync();
            if (requesterDid != did)
            {
                throw new XRPCError(new AuthRequiredErrorDetail($"Missing auth to create account with did: {did}"));
            }

            var handle = await _handle.NormalizeAndValidateHandleAsync(createAccountInput.Handle.Value, did, false);
            var reservedSigningKey = await _reservedSigningKeyStore.ConsumeAsync(did) ?? Secp256k1Keypair.Create(true);

            if (_invitesConfig.Required && createAccountInput.InviteCode != null)
            {
                await _accountRepository.EnsureInviteIsAvailableAsync(createAccountInput.InviteCode);
            }

            var existingAccount = await _accountRepository.GetAccountAsync(did);
            if (existingAccount != null)
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Account already exists"));
            }

            return new ValidatedCreateAccount(
                did,
                handle,
                createAccountInput.Email,
                createAccountInput.Password,
                createAccountInput.InviteCode,
                reservedSigningKey,
                null,
                true);
        }

        var validatedHandle = await _handle.NormalizeAndValidateHandleAsync(createAccountInput.Handle.Value, createAccountInput.Did?.Value, false);
        var signingKey = Secp256k1Keypair.Create(true);

        if (_invitesConfig.Required && createAccountInput.InviteCode != null)
        {
            await _accountRepository.EnsureInviteIsAvailableAsync(createAccountInput.InviteCode);
        }

        var handleAcct = await _accountRepository.GetAccountAsync(validatedHandle);
        var emailAcct = await _accountRepository.GetAccountByEmailAsync(createAccountInput.Email);
        if (handleAcct != null)
        {
            throw new XRPCError(new HandleNotAvailableErrorDetail($"Handle already taken: {validatedHandle}"));
        }

        if (emailAcct != null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Email already taken: {createAccountInput.Email}"));
        }

        var (createdDid, plcOp) = await FormatDidAndPlcOpAsync(validatedHandle, createAccountInput, signingKey);

        return new ValidatedCreateAccount(
            createdDid,
            validatedHandle,
            createAccountInput.Email,
            createAccountInput.Password,
            createAccountInput.InviteCode,
            signingKey,
            plcOp,
            false);
    }

    private async Task<string?> GetRequesterDidAsync()
    {
        if (string.IsNullOrWhiteSpace(Request.Headers.Authorization))
        {
            return null;
        }

        var accessOutput = await _authVerifier.AccessFullAsync(HttpContext);
        return accessOutput.AccessCredentials.Did;
    }

    private async Task<(string Did, SignedOp<AtProtoOp> PlcOp)> FormatDidAndPlcOpAsync(
        string handle,
        CreateAccountInput createAccountInput,
        Secp256k1Keypair signingKey)
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

        var plcCreate = await Operations.CreateOpAsync(
            signingKey.Did(),
            handle,
            _serviceConfig.PublicUrl,
            rotationKeys,
            _secretsConfig.PlcRotationKey);
        return (plcCreate.Did, plcCreate.Op);
    }

    private static string NormalizeUrl(string url)
    {
        return new Uri(url).GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private sealed record ValidatedCreateAccount(
        string Did,
        string Handle,
        string? Email,
        string? Password,
        string? InviteCode,
        Secp256k1Keypair SigningKey,
        SignedOp<AtProtoOp>? PlcOp,
        bool Deactivated);
}
