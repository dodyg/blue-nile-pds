using Microsoft.EntityFrameworkCore;
using Scrypt;

namespace AccountManager.Db;

public class PasswordStore
{
    private readonly AccountManagerDb _db;
    
    public PasswordStore(AccountManagerDb db)
    {
        _db = db;
    }

    public async Task<bool> VerifyAccountPassword(string did, string password)
    {
        var found = await _db.Accounts.FirstOrDefaultAsync(x => x.Did == did);
        if (found == null)
        {
            return false;
        }
        
        var enc = new ScryptEncoder();
       return enc.Compare(password, found.PasswordSCrypt);
    }
}