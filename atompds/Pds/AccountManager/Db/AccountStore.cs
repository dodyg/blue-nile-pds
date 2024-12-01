using atompds.AccountManager;
using atompds.AccountManager.Db;
using atompds.Pds.AccountManager.Db.Schema;
using atompds.Pds.Config;
using Microsoft.EntityFrameworkCore;
using Xrpc;

namespace atompds.Pds.AccountManager.Db;

public record AvailabilityFlags(bool? IncludeTakenDown = null, bool? IncludeDeactivated = null);


public record ActorAccount(string Did, string? Handle, DateTime CreatedAt, string? TakedownRef, 
    DateTime? DeactivatedAt, DateTime? DeleteAfter, string? Email, DateTime? EmailConfirmedAt, bool? InvitesDisabled)
{
    public static ActorAccount? FromActor(Actor? actor, Account? account)
    {
        if (actor == null)
        {
            return null;
        }
            
        return new ActorAccount(actor.Did, actor.Handle, actor.CreatedAt, actor.TakedownRef, actor.DeactivatedAt,
            actor.DeleteAfter, account?.Email, account?.EmailConfirmedAt, account?.InvitesDisabled);
    }
    
    public bool SoftDeleted => TakedownRef != null;
}

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
    
    private record ActorAccTuple(Actor Actor, Account? Account);
    private IQueryable<ActorAccTuple> SelectAccountQb(AvailabilityFlags? flags)
    {
        var actors = _db.Actors
            .AsQueryable();
        if (flags?.IncludeTakenDown != true)
        {
            actors = actors.Where(x => x.TakedownRef != null);
        }
        
        if (flags?.IncludeDeactivated != true)
        {
            actors = actors.Where(x => x.DeactivatedAt == null);
        }
        
        var coll = actors.GroupJoin(_db.Accounts,
            actor => actor.Did,
            account => account.Did,
            (actor, account) => new ActorAccTuple(actor, account.FirstOrDefault()));
        
        return coll;
    }

    public async Task<ActorAccount?> GetAccount(string handleOrDid, AvailabilityFlags? flags = null)
    {
        var actors = SelectAccountQb(flags);
        if (handleOrDid.StartsWith("did:"))
        {
            actors = actors.Where(x => x.Actor.Did == handleOrDid);
        }
        else
        {
            actors = actors.Where(x => x.Actor.Handle == handleOrDid);
        }
        
        var result = await actors.FirstOrDefaultAsync();
        return ActorAccount.FromActor(result?.Actor, result?.Account);
    }

    public async Task<Dictionary<string, ActorAccount>> GetAccounts(string[] dids, AvailabilityFlags? flags = null)
    {
        var actors = SelectAccountQb(flags);
        actors = actors.Where(x => dids.Contains(x.Actor.Did));
        var results = await actors.ToArrayAsync();
        
        return results.ToDictionary(x => x.Actor.Did, x => ActorAccount.FromActor(x.Actor, x.Account)!);
    }
    
    public async Task<ActorAccount?> GetAccountByEmail(string email, AvailabilityFlags? flags = null)
    {
        var actors = SelectAccountQb(flags);
        email = email.ToLower();
        actors = actors
            .Where(x => x.Account != null)
            .Where(x => x.Account!.Email == email);
        
        var result = await actors.FirstOrDefaultAsync();
        return ActorAccount.FromActor(result?.Actor, result?.Account);
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