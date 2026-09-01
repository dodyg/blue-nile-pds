using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PendingAccounts.Models;

namespace PendingAccounts.Services;

public class PendingEmailTokenStore
{
    private readonly PendingAccountsDb _db;

    public PendingEmailTokenStore(PendingAccountsDb db)
    {
        _db = db;
    }

    public async Task<string> CreateTokenAsync(int pendingRegistrationId, PendingEmailTokenPurpose purpose, string? newEmail = null)
    {
        var bytes = RandomNumberGenerator.GetBytes(5);
        var encoded = Convert.ToBase64String(bytes).Replace("+", "A").Replace("/", "B").Replace("=", "C");
        var token = $"{encoded[..4]}-{encoded[4..8]}";
        var now = DateTime.UtcNow;

        var existing = await _db.PendingEmailTokens
            .FirstOrDefaultAsync(t => t.PendingRegistrationId == pendingRegistrationId && t.Purpose == purpose);

        if (existing != null)
        {
            existing.Token = token;
            existing.NewEmail = newEmail;
            existing.RequestedAt = now;
            _db.PendingEmailTokens.Update(existing);
        }
        else
        {
            _db.PendingEmailTokens.Add(new PendingEmailToken
            {
                PendingRegistrationId = pendingRegistrationId,
                Purpose = purpose,
                Token = token,
                NewEmail = newEmail,
                RequestedAt = now
            });
        }

        await _db.SaveChangesAsync();
        return token;
    }

    public async Task AssertValidTokenAsync(int pendingRegistrationId, string token, PendingEmailTokenPurpose purpose)
    {
        var emailToken = await _db.PendingEmailTokens
            .FirstOrDefaultAsync(t =>
                t.PendingRegistrationId == pendingRegistrationId &&
                t.Token == token &&
                t.Purpose == purpose);

        if (emailToken == null)
            throw new InvalidOperationException("Invalid token");

        if (DateTime.UtcNow > emailToken.RequestedAt + TimeSpan.FromMinutes(15))
            throw new InvalidOperationException("Token has expired");
    }

    public Task DeleteTokenAsync(int pendingRegistrationId, PendingEmailTokenPurpose purpose)
    {
        return _db.PendingEmailTokens
            .Where(t => t.PendingRegistrationId == pendingRegistrationId && t.Purpose == purpose)
            .ExecuteDeleteAsync();
    }
}
