using atompds.Pds.AccountManager.Db.Schema;
using Microsoft.EntityFrameworkCore;

namespace atompds.Pds.AccountManager.Db;

public class RepoStore
{
    private readonly AccountManagerDb _db;
    
    public RepoStore(AccountManagerDb db)
    {
        _db = db;
    }

    public async Task UpdateRoot(string did, string cid, string rev)
    {
        var existingRoot = await _db.RepoRoots
            .Where(x => x.Did == did)
            .FirstOrDefaultAsync();

        if (existingRoot == null)
        {
            _db.RepoRoots.Add(new RepoRoot
            {
                Did = did,
                Cid = cid,
                Rev = rev,
                IndexedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingRoot.Cid = cid;
            existingRoot.Rev = rev;
        }
        
        await _db.SaveChangesAsync();
    }
}