using Microsoft.EntityFrameworkCore;
using BlueNilePds.Pds.PendingAccounts.Models;

namespace BlueNilePds.Pds.PendingAccounts;

public class PendingAccountsDb : DbContext
{
    public PendingAccountsDb(DbContextOptions<PendingAccountsDb> options) : base(options)
    {
    }

    public DbSet<PendingRegistration> PendingRegistrations { get; set; }
    public DbSet<PendingProfile> PendingProfiles { get; set; }
    public DbSet<PendingEmailToken> PendingEmailTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PendingRegistration>(e =>
        {
            e.HasIndex(r => r.Email).IsUnique().HasFilter("[Status] = 0");
            e.HasIndex(r => r.Handle).IsUnique().HasFilter("[Status] = 0");
            e.HasIndex(r => r.Status);
        });

        modelBuilder.Entity<PendingProfile>(e =>
        {
            e.HasIndex(p => p.PendingRegistrationId).IsUnique();
        });

        modelBuilder.Entity<PendingEmailToken>(e =>
        {
            e.HasIndex(t => new { t.PendingRegistrationId, t.Purpose });
        });
    }
}
