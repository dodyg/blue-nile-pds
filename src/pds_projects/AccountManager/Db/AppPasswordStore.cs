using Microsoft.EntityFrameworkCore;
using Scrypt;

namespace AccountManager.Db;

public class AppPasswordStore
{
    private readonly AccountManagerDb _db;

    public AppPasswordStore(AccountManagerDb db)
    {
        _db = db;
    }

    public async Task<AppPassword> CreateAppPasswordAsync(string did, string name, string password, bool privileged)
    {
        var enc = new ScryptEncoder();
        var hashed = enc.Encode(password);

        var appPassword = new AppPassword
        {
            Did = did,
            Name = name,
            PasswordSCrypt = hashed,
            CreatedAt = DateTime.UtcNow,
            Privileged = privileged
        };

        _db.AppPasswords.Add(appPassword);
        await _db.SaveChangesAsync();
        return appPassword;
    }

    public async Task<List<AppPassword>> ListAppPasswordsAsync(string did)
    {
        return await _db.AppPasswords
            .Where(ap => ap.Did == did)
            .ToListAsync();
    }

    public async Task<bool> DeleteAppPasswordAsync(string did, string name)
    {
        var count = await _db.AppPasswords
            .Where(ap => ap.Did == did && ap.Name == name)
            .ExecuteDeleteAsync();
        return count > 0;
    }

    public async Task<(bool Valid, bool Privileged)?> VerifyAppPasswordAsync(string did, string password)
    {
        var appPasswords = await _db.AppPasswords
            .Where(ap => ap.Did == did)
            .ToListAsync();

        var enc = new ScryptEncoder();
        foreach (var ap in appPasswords)
        {
            if (enc.Compare(password, ap.PasswordSCrypt))
            {
                return (true, ap.Privileged);
            }
        }

        return null;
    }
}
