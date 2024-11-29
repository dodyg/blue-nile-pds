using atompds.Model;
using FishyFlip.Models;
using Microsoft.EntityFrameworkCore;

namespace atompds.AccountManager.Db;

public class InviteStore
{
    private readonly AccountManagerDb _db;
    public InviteStore(AccountManagerDb db)
    {
        _db = db;
    }

    public async Task EnsureInviteIsAvailable(string code)
    {
        var invite = await _db.InviteCodes
            .Include(x => x.Actor)
            .Where(x => x.Actor.TakedownRef == null)
            .Where(x => x.Code == code)
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
}