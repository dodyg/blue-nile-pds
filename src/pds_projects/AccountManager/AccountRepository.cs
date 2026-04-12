using AccountManager.Db;
using CID;
using Config;
using Scrypt;
using Xrpc;

namespace AccountManager;

public class AuthScopes
{
    public const string AppPass = "com.atproto.appPass";
    public const string AppPassPrivileged = "com.atproto.appPassPrivileged";
}

public class AccountRepository
{
    private readonly AccountStore _accountStore;
    private readonly AppPasswordStore _appPasswordStore;
    private readonly Auth _auth;
    private readonly AccountManagerDb _db;
    private readonly EmailTokenStore _emailTokenStore;
    private readonly InviteStore _inviteStore;
    private readonly PasswordStore _passwordStore;
    private readonly RepoStore _repoStore;
    private readonly SecretsConfig _secretsConfig;
    private readonly ServiceConfig _serviceConfig;
    public AccountRepository(
        ServiceConfig serviceConfig,
        SecretsConfig secretsConfig,
        AccountStore accountStore,
        RepoStore repoStore,
        PasswordStore passwordStore,
        Auth auth,
        InviteStore inviteStore,
        EmailTokenStore emailTokenStore,
        AccountManagerDb db,
        AppPasswordStore appPasswordStore)
    {
        _serviceConfig = serviceConfig;
        _secretsConfig = secretsConfig;
        _accountStore = accountStore;
        _repoStore = repoStore;
        _passwordStore = passwordStore;
        _auth = auth;
        _inviteStore = inviteStore;
        _emailTokenStore = emailTokenStore;
        _db = db;
        _appPasswordStore = appPasswordStore;
    }

    public async Task<string?> GetDidForActorAsync(string repo, AvailabilityFlags? flags = null)
    {
        var account = await _accountStore.GetAccountAsync(repo, flags);
        return account?.Did;
    }

    public async Task<(string AccessJwt, string RefreshJwt)> CreateAccountAsync(string did,
        string handle,
        string? email,
        string? password,
        string repoCid,
        string repoRev,
        string? inviteCode,
        bool? deactivated)
    {
        string? passwordScrypt = null;
        if (password != null)
        {
            var enc = new ScryptEncoder();
            passwordScrypt = enc.Encode(password);
        }

        var tokens = _auth.CreateTokens(did, _secretsConfig.JwtSecret, _serviceConfig.Did, Auth.ACCESS_TOKEN_SCOPE);
        var refreshDecoded = _auth.DecodeRefreshTokenUnsafe(tokens.RefreshToken, _secretsConfig.JwtSecret);
        var now = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(inviteCode))
        {
            // ensure invite code is available
            await _inviteStore.EnsureInviteIsAvailableAsync(inviteCode);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _accountStore.RegisterActorAsync(did, handle, deactivated);
        if (email != null && passwordScrypt != null)
        {
            await _accountStore.RegisterAccountAsync(did, email, passwordScrypt);
        }
        await _inviteStore.RecordInviteUseAsync(did, inviteCode, now);
        await _auth.StoreRefreshTokenAsync(refreshDecoded, null);
        await _repoStore.UpdateRootAsync(did, repoCid, repoRev);
        await transaction.CommitAsync();
        await _db.SaveChangesAsync();

        return (tokens.AccessToken, tokens.RefreshToken);
    }

    public async Task DeleteAccountAsync(string did)
    {
        await _accountStore.DeleteAccountAsync(did);
    }

    public async Task EnsureInviteIsAvailableAsync(string code)
    {
        await _inviteStore.EnsureInviteIsAvailableAsync(code);
    }

    public async Task<ActorAccount?> GetAccountAsync(string handleOrDid, AvailabilityFlags? flags = null)
    {
        return await _accountStore.GetAccountAsync(handleOrDid, flags);
    }

    public async Task<Dictionary<string, ActorAccount>> GetAccountsAsync(string[] dids, AvailabilityFlags? flags = null)
    {
        return await _accountStore.GetAccountsAsync(dids, flags);
    }

    public async Task<ActorAccount?> GetAccountByEmailAsync(string email, AvailabilityFlags? flags = null)
    {
        return await _accountStore.GetAccountByEmailAsync(email, flags);
    }

    public async Task<(string AccessJwt, string RefreshJwt)> CreateSessionAsync(string did, string? appPasswordName = null, string? scope = null)
    {
        var tokenScope = scope ?? Auth.ACCESS_TOKEN_SCOPE;
        var tokens = _auth.CreateTokens(did, _secretsConfig.JwtSecret, _serviceConfig.Did, tokenScope);
        var refreshDecoded = _auth.DecodeRefreshTokenUnsafe(tokens.RefreshToken, _secretsConfig.JwtSecret);
        await _auth.StoreRefreshTokenAsync(refreshDecoded, appPasswordName);
        return (tokens.AccessToken, tokens.RefreshToken);
    }

    public Task<string> CreateEmailTokenAsync(string did, EmailToken.EmailTokenPurpose purpose)
    {
        return _emailTokenStore.CreateEmailTokenAsync(did, purpose);
    }

    public Task AssertValidEmailTokenAsync(string did, string token, EmailToken.EmailTokenPurpose purpose)
    {
        return _emailTokenStore.AssertValidTokenAsync(did, token, purpose);
    }

    public async Task UpdateRepoRootAsync(string did, Cid cid, string rev)
    {
        await _repoStore.UpdateRootAsync(did, cid.ToString(), rev);
    }

    public Task<bool> VerifyAccountPasswordAsync(string did, string appPassword)
    {
        return _passwordStore.VerifyAccountPasswordAsync(did, appPassword);
    }

    public Task<bool> RevokeRefreshTokenAsync(string jti)
    {
        return _auth.RevokeRefreshTokenAsync(jti);
    }

    public async Task RevokeAppPasswordRefreshTokensAsync(string did, string appPasswordName)
    {
        await _auth.RevokeAppPasswordRefreshTokenAsync(did, appPasswordName);
    }

    public async Task<(string AccessJwt, string RefreshJwt)?> RotateRefreshTokenAsync(string tokenId)
    {
        return await _auth.RotateRefreshTokenAsync(tokenId, _secretsConfig.JwtSecret, _serviceConfig.Did);
    }

    public async Task ActivateAccountAsync(string did)
    {
        await _accountStore.ActivateAccountAsync(did);
    }

    public async Task DeactivateAccountAsync(string did, DateTimeOffset? deleteAfter)
    {
        await _accountStore.DeactivateAccountAsync(did, deleteAfter);
    }

    public async Task<AccountStore.AccountStatus> GetAccountStatusAsync(string did)
    {
        return await _accountStore.GetAccountStatusAsync(did);
    }

    public async Task UpdateHandleAsync(string did, string handle)
    {
        await _accountStore.UpdateHandleAsync(did, handle);
    }

    public async Task ConfirmEmailAsync(string did)
    {
        await _accountStore.ConfirmEmailAsync(did);
    }

    public async Task UpdateEmailAsync(string did, string email)
    {
        await _accountStore.UpdateEmailAsync(did, email);
    }

    public async Task UpdatePasswordAsync(string did, string password)
    {
        await _accountStore.UpdatePasswordAsync(did, password);
    }

    public async Task UpdateTakedownRefAsync(string did, string? takedownRef)
    {
        await _accountStore.UpdateTakedownRefAsync(did, takedownRef);
    }

    public async Task UpdateInvitesDisabledAsync(string did, bool disabled)
    {
        await _accountStore.UpdateInvitesDisabledAsync(did, disabled);
    }

    public record LoginResult(ActorAccount Account, string? AppPasswordName, string? AppPasswordScope);

    public async Task<LoginResult> LoginAsync(string identifier, string password)
    {
        var start = DateTime.UtcNow;
        try
        {
            var identifierNormalized = identifier.ToLower();

            ActorAccount? user;
            if (identifierNormalized.Contains("@"))
            {
                user = await GetAccountByEmailAsync(identifierNormalized, new AvailabilityFlags(true, true));
            }
            else
            {
                user = await GetAccountAsync(identifierNormalized, new AvailabilityFlags(true, true));
            }

            if (user == null)
            {
                throw new XRPCError(new AuthRequiredErrorDetail("Invalid username or password"));
            }

            var validAccountPass = await _passwordStore.VerifyAccountPasswordAsync(user.Did, password);
            if (validAccountPass)
            {
                if (user.SoftDeleted)
                {
                    throw new XRPCError(new AccountTakenDownErrorDetail("Account has been taken down"));
                }

                return new LoginResult(user, null, null);
            }

            var appPassResult = await _appPasswordStore.VerifyAppPasswordAsync(user.Did, password);
            if (appPassResult != null && appPassResult.Value.Valid)
            {
                if (user.SoftDeleted)
                {
                    throw new XRPCError(new AccountTakenDownErrorDetail("Account has been taken down"));
                }

                var scope = appPassResult.Value.Privileged
                    ? AuthScopes.AppPassPrivileged
                    : AuthScopes.AppPass;
                return new LoginResult(user, "app-password", scope);
            }

            throw new XRPCError(new AuthRequiredErrorDetail("Invalid username or password"));
        }
        finally
        {
            var delay = Math.Max(0, 350 - (DateTime.UtcNow - start).Milliseconds);
            await Task.Delay(delay);
        }
    }
}
