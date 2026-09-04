using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlueNilePds.Pds.PendingAccounts.Design;

public class PendingAccountsDbDesignFactory : IDesignTimeDbContextFactory<PendingAccountsDb>
{
    public PendingAccountsDb CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PendingAccountsDb>();
        optionsBuilder.UseSqlite("Data Source=stub-pending.db");
        return new PendingAccountsDb(optionsBuilder.Options);
    }
}
