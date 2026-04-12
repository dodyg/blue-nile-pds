using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Xrpc;

namespace AccountManager.Db;

public class InviteStore
{
    private readonly AccountManagerDb _db;
    public InviteStore(AccountManagerDb db)
    {
        _db = db;
    }

    public async Task EnsureInviteIsAvailableAsync(string code)
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
            throw new XRPCError(new InvalidInviteCodeErrorDetail("Provided invite code is not available"));
        }

        var uses = await _db.InviteCodeUses
            .CountAsync(x => x.Code == code);

        if (invite.AvailableUses <= uses)
        {
            throw new XRPCError(new InvalidInviteCodeErrorDetail("Provided invite code not available"));
        }
    }

    public async Task RecordInviteUseAsync(string did, string? inviteCode, DateTime now)
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

    public async Task<string> CreateInviteCodeAsync(string forAccount, string createdBy, int useCount)
    {
        var code = GenerateInviteCode();
        _db.InviteCodes.Add(new InviteCode
        {
            Code = code,
            AvailableUses = useCount,
            Disabled = false,
            ForAccount = forAccount,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return code;
    }

    public async Task<List<InviteCode>> GetInviteCodesForAccountAsync(string did)
    {
        return await _db.InviteCodes
            .Where(ic => ic.ForAccount == did)
            .ToListAsync();
    }

    private static string GenerateInviteCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[8];
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLower();
    }
}