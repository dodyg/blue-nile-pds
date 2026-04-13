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

    public async Task<(List<InviteCode> Codes, string? Cursor)> GetInviteCodesAsync(string sort, int limit, string? cursor)
    {
        var offset = 0;
        if (!string.IsNullOrWhiteSpace(cursor) && !int.TryParse(cursor, out offset))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Malformed cursor"));
        }

        IQueryable<InviteCode> query = _db.InviteCodes;
        query = sort switch
        {
            "recent" => query
                .OrderByDescending(invite => invite.CreatedAt)
                .ThenBy(invite => invite.Code),
            "usage" => query
                .GroupJoin(
                    _db.InviteCodeUses,
                    invite => invite.Code,
                    use => use.Code,
                    (invite, uses) => new { Invite = invite, Uses = uses.Count() })
                .OrderByDescending(row => row.Uses)
                .ThenBy(row => row.Invite.Code)
                .Select(row => row.Invite),
            _ => throw new XRPCError(new InvalidRequestErrorDetail($"unknown sort method: {sort}"))
        };

        var results = await query
            .Skip(offset)
            .Take(limit + 1)
            .ToListAsync();
        var nextCursor = results.Count > limit ? (offset + limit).ToString() : null;

        return (results.Take(limit).ToList(), nextCursor);
    }

    public async Task<Dictionary<string, List<InviteCodeUse>>> GetInviteCodeUsesAsync(IEnumerable<string> codes)
    {
        var codeList = codes.ToArray();
        if (codeList.Length == 0)
        {
            return new Dictionary<string, List<InviteCodeUse>>();
        }

        var uses = await _db.InviteCodeUses
            .Where(use => codeList.Contains(use.Code))
            .OrderBy(use => use.UsedAt)
            .ToListAsync();

        return uses
            .GroupBy(use => use.Code)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public async Task DisableInviteCodesAsync(IEnumerable<string> codes, IEnumerable<string> accounts)
    {
        var codeList = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var accountList = accounts
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (codeList.Length == 0 && accountList.Length == 0)
        {
            return;
        }

        var invites = await _db.InviteCodes
            .Where(invite => codeList.Contains(invite.Code) || accountList.Contains(invite.ForAccount))
            .ToListAsync();

        foreach (var invite in invites)
        {
            invite.Disabled = true;
        }

        await _db.SaveChangesAsync();
    }

    private static string GenerateInviteCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[8];
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLower();
    }
}
