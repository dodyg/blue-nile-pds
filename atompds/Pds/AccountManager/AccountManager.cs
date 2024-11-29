using atompds.AccountManager.Db;
using atompds.Pds.AccountManager.Db;
using atompds.Pds.Config;
using Scrypt;

namespace atompds.AccountManager;

public class AccountManager
{
    private readonly ServiceConfig _serviceConfig;
    private readonly SecretsConfig _secretsConfig;
    private readonly AccountStore _accountStore;
    private readonly RepoStore _repoStore;
    private readonly Auth _auth;
    private readonly InviteStore _inviteStore;
    private readonly AccountManagerDb _db;
    public AccountManager(ServiceConfig serviceConfig, SecretsConfig secretsConfig, 
        AccountStore accountStore,
        RepoStore repoStore,
        Auth auth, InviteStore inviteStore, AccountManagerDb db)
    {
        _serviceConfig = serviceConfig;
        _secretsConfig = secretsConfig;
        _accountStore = accountStore;
        _repoStore = repoStore;
        _auth = auth;
        _inviteStore = inviteStore;
        _db = db;
    }
    
    public async Task<(string AccessJwt, string RefreshJwt)> CreateAccount(string did, string handle, string? email, string? password, string repoCid, string repoRev, string? inviteCode, bool? deactivated)
    {
        string? passwordScrypt = null;
        if (password != null)
        {
            var enc = new ScryptEncoder();
            passwordScrypt = enc.Encode(password);
        }

        var tokens = _auth.CreateTokens(did, _secretsConfig.JwtSecret, _serviceConfig.Did, Auth.ACCESS_TOKEN_SCOPE);
        var refreshDecoded = _auth.DecodeRefreshToken(tokens.RefreshToken, _secretsConfig.JwtSecret);
        var now = DateTime.UtcNow;

        if (inviteCode != null)
        {
            // ensure invite code is available
            await _inviteStore.EnsureInviteIsAvailable(inviteCode);
        }
        
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _accountStore.RegisterActor(did, handle, deactivated);
        if (email != null && passwordScrypt != null)
        {
            await _accountStore.RegisterAccount(did, email, passwordScrypt);
        }
        await _inviteStore.RecordInviteUse(did, inviteCode, now);
        await _auth.StoreRefreshToken(refreshDecoded, null);
        await _repoStore.UpdateRoot(did, repoCid, repoRev);
        await transaction.CommitAsync();
        await _db.SaveChangesAsync();
        
        return (tokens.AccessToken, tokens.RefreshToken);
    }
    
    public async Task EnsureInviteIsAvailable(string code)
    {
        await _inviteStore.EnsureInviteIsAvailable(code);
    }
    
    public async Task<AccountStore.ActorAccount?> GetAccount(string handleOrDid, AvailabilityFlags? flags = null)
    {
        return await _accountStore.GetAccount(handleOrDid, flags);
    }
    
    public async Task<AccountStore.ActorAccount?> GetAccountByEmail(string email, AvailabilityFlags? flags = null)
    {
        return await _accountStore.GetAccountByEmail(email, flags);
    }
}