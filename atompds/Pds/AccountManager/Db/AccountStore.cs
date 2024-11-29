using atompds.AccountManager.Db.Schema;
using atompds.Config;
using Microsoft.EntityFrameworkCore;
using Scrypt;

namespace atompds.AccountManager.Db;

public record AvailabilityFlags(bool? IncludeTakenDown = null, bool? IncludeDeactivated = null);

public class AccountStore
{
    private readonly AccountManagerDb _db;
    private readonly Auth _auth;
    private readonly SecretsConfig _secretsConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly InviteStore _inviteStore;
    public AccountStore(AccountManagerDb db, Auth auth, SecretsConfig secretsConfig, ServiceConfig serviceConfig, InviteStore inviteStore)
    {
        _db = db;
        _auth = auth;
        _secretsConfig = secretsConfig;
        _serviceConfig = serviceConfig;
        _inviteStore = inviteStore;
    }
    
    public record ActorAccount(string Did, string? Handle, DateTime CreatedAt, string? TakedownRef, 
        DateTime? DeactivatedAt, DateTime? DeleteAfter, string? Email, DateTime? EmailConfirmedAt, bool? InvitesDisabled)
    {
        public static ActorAccount? FromActor(Actor? actor)
        {
            if (actor == null)
            {
                return null;
            }
            
            return new ActorAccount(actor.Did, actor.Handle, actor.CreatedAt, actor.TakedownRef, actor.DeactivatedAt,
                actor.DeleteAfter, actor.Account.Email, actor.Account.EmailConfirmedAt, actor.Account.InvitesDisabled);
        }
    }
    
    private IQueryable<Actor> SelectAccountQb(AvailabilityFlags? flags)
    {
        var actors = _db.Actors
            .Include(x => x.Account)
            .AsQueryable();
        if (flags?.IncludeTakenDown != true)
        {
            actors = actors.Where(x => x.TakedownRef != null);
        }
        
        if (flags?.IncludeDeactivated != true)
        {
            actors = actors.Where(x => x.DeactivatedAt == null);
        }
        
        return actors;
    }

    public async Task<ActorAccount?> GetAccount(string handleOrDid, AvailabilityFlags? flags = null)
    {
        var actors = SelectAccountQb(flags);
        if (handleOrDid.StartsWith("did:"))
        {
            actors = actors.Where(x => x.Did == handleOrDid);
        }
        else
        {
            actors = actors.Where(x => x.Handle == handleOrDid);
        }
        
        var result = await actors.FirstOrDefaultAsync();
        return ActorAccount.FromActor(result);
    }

    public async Task<Dictionary<string, ActorAccount>> GetAccounts(string[] dids, AvailabilityFlags? flags = null)
    {
        var actors = SelectAccountQb(flags);
        actors = actors.Where(x => dids.Contains(x.Did));
        var results = await actors.ToArrayAsync();
        
        return results.ToDictionary(x => x.Did, x => ActorAccount.FromActor(x)!);
    }
    
    public async Task<ActorAccount?> GetAccountByEmail(string email, AvailabilityFlags? flags = null)
    {
        var actors = SelectAccountQb(flags);
        email = email.ToLower();
        actors = actors.Where(x => x.Account.Email == email);
        
        var result = await actors.FirstOrDefaultAsync();
        return ActorAccount.FromActor(result);
    }
    
    public async Task<bool> IsAccountActivated(string did)
    {
        var account = await GetAccount(did, new AvailabilityFlags(IncludeTakenDown: true));
        if (account == null)
        {
            return false;
        }
        
        return account.DeactivatedAt == null;
    }
    
    public async Task<string?> GetDidForActor(string handle)
    {
        var account = await GetAccount(handle);
        return account?.Did;
    }
    
    public async Task<AccountStatus> GetAccountStatus(string did)
    {
        var account = await GetAccount(did, new AvailabilityFlags(IncludeTakenDown: true, IncludeDeactivated: true));
        if (account == null) return AccountStatus.Deleted;
        if (account.TakedownRef != null) return AccountStatus.Takendown;
        if (account.DeactivatedAt != null) return AccountStatus.Deactivated;
        return AccountStatus.Active;
    }

    public async Task CreateAccount(string did, string handle, string? email, string? password, string repoCid, string repoRev, string? inviteCode, bool? deactivated)
    {
        string? passwordScrypt = null;
        if (password != null)
        {
            var enc = new ScryptEncoder();
            passwordScrypt = enc.Encode(password);
        }

        var tokens = _auth.CreateTokens(did, _secretsConfig.JwtSecret, _serviceConfig.Did, Auth.ACCESS_TOKEN_SCOPE);
        var now = DateTime.UtcNow;

        if (inviteCode != null)
        {
            // ensure invite code is available
            await _inviteStore.EnsureInviteIsAvailable(inviteCode);
        }
    }

    public enum AccountStatus
    {
        Active,
        Takendown,
        Suspended,
        Deleted,
        Deactivated
    }
}