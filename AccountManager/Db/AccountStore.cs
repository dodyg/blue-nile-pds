using Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xrpc;

namespace AccountManager.Db;

public class AccountStore
{
    private readonly AccountManagerDb _db;
    private readonly Auth _auth;
    private readonly SecretsConfig _secretsConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly InviteStore _inviteStore;
    private readonly ILogger<AccountStore> _logger;
    public AccountStore(AccountManagerDb db, Auth auth, SecretsConfig secretsConfig, 
        ServiceConfig serviceConfig, InviteStore inviteStore, ILogger<AccountStore> logger)
    {
        _db = db;
        _auth = auth;
        _secretsConfig = secretsConfig;
        _serviceConfig = serviceConfig;
        _inviteStore = inviteStore;
        _logger = logger;
    }

    private IQueryable<Account> SelectAccountQb(AvailabilityFlags? flags)
    {
        flags ??= new AvailabilityFlags();
        var accounts = _db.Accounts
            .Include(a => a.Actor)
            .AsQueryable();
        if (!flags.IncludeTakenDown)
        {
            accounts = accounts.Where(x => x.Actor.TakedownRef == null);
        }
        
        if (!flags.IncludeDeactivated)
        {
            accounts = accounts.Where(x => x.Actor.DeactivatedAt == null);
        }

        return accounts;
    }

    public async Task<ActorAccount?> GetAccount(string handleOrDid, AvailabilityFlags? flags = null)
    {
        var accounts = SelectAccountQb(flags);
        if (handleOrDid.StartsWith("did:"))
        {
            accounts = accounts.Where(x => x.Actor.Did == handleOrDid);
        }
        else
        {
            accounts = accounts.Where(x => x.Actor.Handle == handleOrDid);
        }
        
        var result = await accounts.FirstOrDefaultAsync();
        return ActorAccount.From(result?.Actor, result);
    }

    public async Task<Dictionary<string, ActorAccount>> GetAccounts(string[] dids, AvailabilityFlags? flags = null)
    {
        var actors = SelectAccountQb(flags);
        actors = actors.Where(x => dids.Contains(x.Actor.Did));
        var results = await actors.ToArrayAsync();
        
        return results.ToDictionary(x => x.Actor.Did, x => ActorAccount.From(x.Actor, x)!);
    }
    
    public async Task<ActorAccount?> GetAccountByEmail(string email, AvailabilityFlags? flags = null)
    {
        var accounts = SelectAccountQb(flags);
        email = email.ToLower();
        accounts = accounts
            .Where(x => x.Email == email);
        
        var result = await accounts.FirstOrDefaultAsync();
        return ActorAccount.From(result?.Actor, result);
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
    
    public async Task RegisterActor(string did, string handle, bool? deactivated)
    {
        var createdAt = DateTime.UtcNow;
        try
        {
            var actorObj = new Actor
            {
                Did = did,
                Handle = handle,
                CreatedAt = createdAt,
                DeactivatedAt = deactivated == true ? createdAt : null,
                DeleteAfter = deactivated == true ? createdAt.AddDays(3) : null,
                TakedownRef = null
            };
            _db.Actors.Add(actorObj);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException e)
        {
            _logger.LogError(e, "Failed to register actor");
            throw new XRPCError(new InvalidRequestErrorDetail("User already exists"));
        }
    }
    
    public async Task RegisterAccount(string did, string email, string passwordScrypt)
    {
        try
        {
            var accountObj = new Account
            {
                Did = did,
                Email = email.ToLower(),
                PasswordSCrypt = passwordScrypt,
                EmailConfirmedAt = null,
                InvitesDisabled = false
            };
            _db.Accounts.Add(accountObj);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException e)
        {
            _logger.LogError(e, "Failed to register account");
            throw new XRPCError(new InvalidRequestErrorDetail("Account already exists"));
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