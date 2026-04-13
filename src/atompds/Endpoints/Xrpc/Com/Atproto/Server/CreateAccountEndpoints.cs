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
using Microsoft.Data.Sqlite;
using Sequencer;
using Xrpc;
using Operations = DidLib.Operations;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class CreateAccountEndpoints
{
    public static RouteGroupBuilder MapCreateAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.createAccount", HandleAsync).RequireRateLimiting("auth-sensitive");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        CreateAccountInput request,
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
        EntrywayRelayService entrywayRelayService,
        ILogger<Program> logger)
    {
        string? validatedDid = null;
        SqliteConnection? conn = null;
        try
        {
            if (!entrywayRelayService.IsConfigured)
                await captchaVerifier.VerifyAsync(request.VerificationCode);

            var validatedInputs = entrywayRelayService.IsConfigured
                ? await ValidateInputsForEntrywayPdsAsync(request, handle, serviceConfig, secretsConfig, identityConfig, reservedSigningKeyStore, accountRepository)
                : await ValidateInputsForLocalPdsAsync(request, context, authVerifier, invitesConfig, emailAddressValidator, handle, accountRepository, reservedSigningKeyStore, identityConfig, serviceConfig, secretsConfig);
            validatedDid = validatedInputs.Did;

            await using var actorStoreDb = actorRepositoryProvider.Create(validatedInputs.Did, validatedInputs.SigningKey);
            conn = actorStoreDb.Connection;
            var commit = await actorStoreDb.TransactRepoAsync(async repo => await repo.Repo.CreateRepoAsync([]));

            if (validatedInputs.PlcOp != null)
            {
                try
                {
                    await plcClient.SendOperationAsync(validatedInputs.Did, validatedInputs.PlcOp);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to create did:plc for {didKey}, {handle}", validatedInputs.Did, validatedInputs.Handle);
                    throw new XRPCError(new InvalidRequestErrorDetail("Failed to create did:plc"), e);
                }
            }

            var didDoc = await SafeResolveDidDocAsync(validatedInputs.Did, true, idResolver, logger);
            var creds = await accountRepository.CreateAccountAsync(
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
                await sequencer.SequenceIdentityEventAsync(validatedInputs.Did, validatedInputs.Handle);
                await sequencer.SequenceAccountEventAsync(validatedInputs.Did, AccountStore.AccountStatus.Active);
                await sequencer.SequenceCommitAsync(validatedInputs.Did, commit, []);
            }

            await accountRepository.UpdateRepoRootAsync(validatedInputs.Did, commit.Cid, commit.Rev);

            return Results.Ok(new CreateAccountOutput
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
            logger.LogError(e, "Failed to create account");
            if (!string.IsNullOrWhiteSpace(validatedDid))
            {
                if (conn != null) SqliteConnection.ClearPool(conn);
                actorRepositoryProvider.Destroy(validatedDid);
            }
            throw;
        }
    }

    private static async Task<DidDocument?> SafeResolveDidDocAsync(string did, bool forceRefresh, IdResolver idResolver, ILogger logger)
    {
        try { return await idResolver.DidResolver.ResolveAsync(did, forceRefresh); }
        catch (Exception e) { logger.LogWarning(e, "Failed to resolve did doc: {did}", did); return null; }
    }

    private static async Task<ValidatedCreateAccount> ValidateInputsForEntrywayPdsAsync(
        CreateAccountInput createAccountInput,
        HandleManager handleManager,
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig,
        IdentityConfig identityConfig,
        ReservedSigningKeyStore reservedSigningKeyStore,
        AccountRepository accountRepository)
    {
        if (createAccountInput.Did == null || createAccountInput.PlcOp == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Entryway account creation requires \"did\" and \"plcOp\""));

        var did = createAccountInput.Did.Value;
        var plcOp = JsonSerializer.Deserialize<SignedOp<AtProtoOp>>(createAccountInput.PlcOp.Value.GetRawText())
            ?? throw new XRPCError(new IncompatibleDidDocErrorDetail("invalid plc operation"));
        var handle = await handleManager.NormalizeAndValidateHandleAsync(createAccountInput.Handle.Value, did, false);

        if (!string.Equals(plcOp.Op.Type, "plc_operation", StringComparison.Ordinal) || plcOp.Op.Prev != null)
            throw new XRPCError(new IncompatibleDidDocErrorDetail("invalid plc operation"));

        var derivedDid = await Operations.DidForCreateOpAsync(plcOp);
        if (!string.Equals(derivedDid, did, StringComparison.Ordinal))
            throw new XRPCError(new IncompatibleDidDocErrorDetail("provided DID does not match plcOp"));

        if (!plcOp.Op.VerificationMethods.TryGetValue("atproto", out var signingDid) || string.IsNullOrWhiteSpace(signingDid))
            throw new XRPCError(new IncompatibleDidDocErrorDetail("provided DID document is missing an atproto signing key"));

        var reservedSigningKey = await reservedSigningKeyStore.ConsumeAsync(did)
            ?? await reservedSigningKeyStore.ConsumeAsync(signingDid);
        if (reservedSigningKey == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Reserved signing key does not exist"));

        if (!string.Equals(reservedSigningKey.Did(), signingDid, StringComparison.Ordinal))
            throw new XRPCError(new IncompatibleDidDocErrorDetail("DID document signing key does not match reserved signing key"));

        if (!plcOp.Op.AlsoKnownAs.Contains($"at://{handle}", StringComparer.Ordinal))
            throw new XRPCError(new IncompatibleDidDocErrorDetail("provided handle does not match DID document handle"));

        if (!plcOp.Op.Services.TryGetValue("atproto_pds", out var pdsService) ||
            !string.Equals(pdsService.Type, "AtprotoPersonalDataServer", StringComparison.Ordinal) ||
            !string.Equals(NormalizeUrl(pdsService.Endpoint), NormalizeUrl(serviceConfig.PublicUrl), StringComparison.Ordinal))
        {
            throw new XRPCError(new IncompatibleDidDocErrorDetail("DID document pds endpoint does not match service endpoint"));
        }

        return new ValidatedCreateAccount(did, handle, null, null, null, reservedSigningKey, plcOp, false);
    }

    private static async Task<ValidatedCreateAccount> ValidateInputsForLocalPdsAsync(
        CreateAccountInput createAccountInput,
        HttpContext context,
        AuthVerifier authVerifier,
        InvitesConfig invitesConfig,
        EmailAddressValidator emailAddressValidator,
        HandleManager handleManager,
        AccountRepository accountRepository,
        ReservedSigningKeyStore reservedSigningKeyStore,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig)
    {
        if (createAccountInput.PlcOp != null)
            throw new XRPCError(new InvalidRequestErrorDetail("Unsupported input: \"plcOp\""));

        if (!string.IsNullOrWhiteSpace(createAccountInput.VerificationPhone))
            throw new XRPCError(new InvalidRequestErrorDetail("Unsupported input: \"verificationPhone\""));

        if (invitesConfig.Required && string.IsNullOrWhiteSpace(createAccountInput.InviteCode))
            throw new XRPCError(new InvalidInviteCodeErrorDetail("No invite code provided"));

        if (createAccountInput.Email == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Email is required"));

        await emailAddressValidator.AssertSupportedEmailAsync(createAccountInput.Email);

        if (createAccountInput.Did != null)
        {
            var did = createAccountInput.Did.Value;
            var requesterDid = await GetRequesterDidAsync(context, authVerifier);
            if (requesterDid != did)
                throw new XRPCError(new AuthRequiredErrorDetail($"Missing auth to create account with did: {did}"));

            var handle = await handleManager.NormalizeAndValidateHandleAsync(createAccountInput.Handle.Value, did, false);
            var reservedSigningKey = await reservedSigningKeyStore.ConsumeAsync(did) ?? Secp256k1Keypair.Create(true);

            if (invitesConfig.Required && createAccountInput.InviteCode != null)
                await accountRepository.EnsureInviteIsAvailableAsync(createAccountInput.InviteCode);

            var existingAccount = await accountRepository.GetAccountAsync(did);
            if (existingAccount != null)
                throw new XRPCError(new InvalidRequestErrorDetail("Account already exists"));

            return new ValidatedCreateAccount(did, handle, createAccountInput.Email, createAccountInput.Password, createAccountInput.InviteCode, reservedSigningKey, null, true);
        }

        var validatedHandle = await handleManager.NormalizeAndValidateHandleAsync(createAccountInput.Handle.Value, createAccountInput.Did?.Value, false);
        var signingKey = Secp256k1Keypair.Create(true);

        if (invitesConfig.Required && createAccountInput.InviteCode != null)
            await accountRepository.EnsureInviteIsAvailableAsync(createAccountInput.InviteCode);

        var handleAcct = await accountRepository.GetAccountAsync(validatedHandle);
        var emailAcct = await accountRepository.GetAccountByEmailAsync(createAccountInput.Email);
        if (handleAcct != null)
            throw new XRPCError(new HandleNotAvailableErrorDetail($"Handle already taken: {validatedHandle}"));
        if (emailAcct != null)
            throw new XRPCError(new InvalidRequestErrorDetail($"Email already taken: {createAccountInput.Email}"));

        var (createdDid, plcOp) = await FormatDidAndPlcOpAsync(validatedHandle, createAccountInput, signingKey, identityConfig, serviceConfig, secretsConfig);

        return new ValidatedCreateAccount(createdDid, validatedHandle, createAccountInput.Email, createAccountInput.Password, createAccountInput.InviteCode, signingKey, plcOp, false);
    }

    private static async Task<string?> GetRequesterDidAsync(HttpContext context, AuthVerifier authVerifier)
    {
        if (string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
            return null;

        var accessOutput = await authVerifier.AccessFullAsync(context);
        return accessOutput.AccessCredentials.Did;
    }

    private static async Task<(string Did, SignedOp<AtProtoOp> PlcOp)> FormatDidAndPlcOpAsync(
        string handle,
        CreateAccountInput createAccountInput,
        Secp256k1Keypair signingKey,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig)
    {
        string[] rotationKeys = [secretsConfig.PlcRotationKey.Did()];
        if (identityConfig.RecoveryDidKey != null)
            rotationKeys = [identityConfig.RecoveryDidKey, ..rotationKeys];
        if (createAccountInput.RecoveryKey != null)
            rotationKeys = [createAccountInput.RecoveryKey, ..rotationKeys];

        var plcCreate = await Operations.CreateOpAsync(signingKey.Did(), handle, serviceConfig.PublicUrl, rotationKeys, secretsConfig.PlcRotationKey);
        return (plcCreate.Did, plcCreate.Op);
    }

    private static string NormalizeUrl(string url) => new Uri(url).GetLeftPart(UriPartial.Path).TrimEnd('/');

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
