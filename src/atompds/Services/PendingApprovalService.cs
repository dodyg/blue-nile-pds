using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using ActorStore.Repo;
using BlueNilePds.Config;
using BlueNilePds.Utils;
using Config;
using Crypto.Secp256k1;
using DidLib;
using Identity;
using Repo;
using Sequencer;

namespace BlueNilePds.Services;

public class PendingApprovalService
{
    private readonly AccountRepository _accountRepository;
    private readonly ActorRepositoryProvider _actorRepositoryProvider;
    private readonly SequencerRepository _sequencer;
    private readonly PlcClient _plcClient;
    private readonly IdentityConfig _identityConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly SecretsConfig _secretsConfig;
    private readonly ReservedSigningKeyStore _reservedSigningKeyStore;

    public PendingApprovalService(
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        SequencerRepository sequencer,
        PlcClient plcClient,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig,
        ReservedSigningKeyStore reservedSigningKeyStore)
    {
        _accountRepository = accountRepository;
        _actorRepositoryProvider = actorRepositoryProvider;
        _sequencer = sequencer;
        _plcClient = plcClient;
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _secretsConfig = secretsConfig;
        _reservedSigningKeyStore = reservedSigningKeyStore;
    }

    public async Task<ApprovalResult> CreatePdsAccountAsync(
        string handle,
        string email,
        string passwordScrypt,
        string? inviteCode,
        string? location,
        string? accountType)
    {
        var signingKey = Secp256k1Keypair.Create(true);

        string[] rotationKeys = [_secretsConfig.PlcRotationKey.Did()];
        if (_identityConfig.RecoveryDidKey != null)
            rotationKeys = [_identityConfig.RecoveryDidKey, ..rotationKeys];
        if (_identityConfig.EntrywayPlcRotationKey != null)
            rotationKeys = [_identityConfig.EntrywayPlcRotationKey, ..rotationKeys];

        var plcCreate = await DidLib.Operations.CreateOpAsync(signingKey.Did(), handle, _serviceConfig.PublicUrl, rotationKeys, _secretsConfig.PlcRotationKey);
        var did = plcCreate.Did;
        var plcOp = plcCreate.Op;

        await _plcClient.SendOperationAsync(did, plcOp);

        var writes = BuildInitialWrites(did, location, accountType);
        await using var actorStoreDb = _actorRepositoryProvider.Create(did, signingKey);
        var commit = await actorStoreDb.TransactRepoAsync(async repo => await repo.Repo.CreateRepoAsync(writes));

        await _accountRepository.CreateAccountAsync(
            did, handle, email, passwordScrypt, commit.Cid.ToString(), commit.Rev,
            inviteCode, false, location, accountType, false, alreadyHashed: true);

        await _sequencer.SequenceIdentityEventAsync(did, handle);
        await _sequencer.SequenceAccountEventAsync(did, AccountStore.AccountStatus.Active);
        await _sequencer.SequenceCommitAsync(did, commit, writes);

        await _accountRepository.UpdateRepoRootAsync(did, commit.Cid, commit.Rev);

        return new ApprovalResult(did, handle);
    }

    private static PreparedCreate[] BuildInitialWrites(string did, string? location, string? accountType)
    {
        var record = new Dictionary<string, object?>
        {
            ["$type"] = "africa.bsky.account",
            ["createdAt"] = DateTime.UtcNow.ToString("O")
        };
        if (location != null) record["location"] = location;
        if (accountType != null) record["accountType"] = accountType;

        return [Prepare.PrepareCreate(did, "africa.bsky.account", "self", null, JsonSerializer.SerializeToElement(record), null)];
    }
}

public record ApprovalResult(string Did, string Handle);
