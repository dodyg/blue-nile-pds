using atompds.AccountManager.Db;
using atompds.Model;
using atompds.Pds.AccountManager.Db.Schema;
using Microsoft.EntityFrameworkCore;

namespace atompds.Pds.AccountManager.Db;

public class InviteStore
{
    private readonly AccountManagerDb _db;
    public InviteStore(AccountManagerDb db)
    {
        _db = db;
    }

    public async Task EnsureInviteIsAvailable(string code)
    {
        var inviteActors = _db.InviteCodes.GroupJoin(_db.Actors,
            invite => invite.ForAccount,
            actor => actor.Did,
            (invite, actors) => new {Invite = invite, Actors = actors});
        
        var invite = await inviteActors
            .Where(x => x.Actors.All(a => a.TakedownRef == null))
            .Where(x => x.Invite.Code == code)
            .Select(x => x.Invite)
            .FirstOrDefaultAsync();

        if (invite == null || invite.Disabled)
        {
            throw new ErrorDetailException(new InvalidInviteCodeErrorDetail("Provided invite code is not available"));
        }

        var uses = await _db.InviteCodeUses
            .CountAsync(x => x.Code == code);

        if (invite.AvailableUses <= uses)
        {
            throw new ErrorDetailException(new InvalidInviteCodeErrorDetail("Provided invite code not available"));
        }
    }
    
    public async Task RecordInviteUse(string did, string? inviteCode, DateTime now)
    {
        if (inviteCode == null)
        {
            return;
        }
        
        await _db.InviteCodeUses.AddAsync(new InviteCodeUse
        {
            UsedBy = did,
            Code = inviteCode,
            UsedAt = now
        });
        
        await _db.SaveChangesAsync();
    } 
}