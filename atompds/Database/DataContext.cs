using atompds.Model;
using Microsoft.EntityFrameworkCore;

namespace atompds.Database;

public class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
    }

    public DbSet<AccountRecord> Accounts { get; init; }
    public DbSet<ConfigRecord> Config { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountRecord>().HasKey(x => x.Did);
        modelBuilder.Entity<ConfigRecord>().HasKey(x => x.PdsDid);
    }

    public async Task SetupAsync()
    {
        //await Database.MigrateAsync();
        await Database.EnsureCreatedAsync();
    }
}