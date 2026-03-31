using AccountManager.Db;
using CID;
using Config;
using Scrypt;
using Xrpc;

namespace AccountManager;

public class AccountRepository
{
    private readonly AccountStore _accountStore;
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
        AccountManagerDb db)
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

    public async Task<ActorAccount?> GetAccountByEmailAsync(string email, AvailabilityFlags? flags = null)
    {
        return await _accountStore.GetAccountByEmailAsync(email, flags);
    }

    public async Task<(string AccessJwt, string RefreshJwt)> CreateSessionAsync(string did, string? appPassword = null)
    {
        // TODO: App password support.
        // scope=auth.formatScope(appPassword)
        var tokens = _auth.CreateTokens(did, _secretsConfig.JwtSecret, _serviceConfig.Did, Auth.ACCESS_TOKEN_SCOPE);
        var refreshDecoded = _auth.DecodeRefreshTokenUnsafe(tokens.RefreshToken, _secretsConfig.JwtSecret);
        await _auth.StoreRefreshTokenAsync(refreshDecoded, appPassword);
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

    public async Task<ActorAccount> LoginAsync(string identifier, string password)
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
            if (!validAccountPass)
            {
                // TODO: App password validation if acc password fails.
                throw new XRPCError(new AuthRequiredErrorDetail("Invalid username or password"));
            }

            if (user.SoftDeleted)
            {
                throw new XRPCError(new AccountTakenDownErrorDetail("Account has been taken down"));
            }

            return user;
        }
        finally
        {
            // mitigate timing attacks
            var delay = Math.Max(0, 350 - (DateTime.UtcNow - start).Milliseconds);
            await Task.Delay(delay);
        }
    }
}
