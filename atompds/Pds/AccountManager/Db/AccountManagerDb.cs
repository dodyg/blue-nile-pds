using atompds.Pds;
using atompds.Pds.AccountManager.Db.Schema;
using Microsoft.EntityFrameworkCore;

namespace atompds.AccountManager.Db;

public class AccountManagerDb : DbContext
{
    public AccountManagerDb(DbContextOptions<AccountManagerDb> options) : base(options)
    {
    }
    
    public DbSet<Actor> Actors { get; set; }
    public DbSet<Account> Accounts { get; set; }
    //public DbSet<Device> Devices { get; set; }
    //public DbSet<DeviceAccount> DeviceAccounts { get; set; }
    //public DbSet<AuthorizationRequest> AuthorizationRequests { get; set; }
    //public DbSet<Token> Tokens { get; set; }
    public DbSet<Pds.AccountManager.Db.Schema.RefreshToken> RefreshTokens { get; set; }
    //public DbSet<UsedRefreshToken> UsedRefreshTokens { get; set; }
    public DbSet<AppPassword> AppPasswords { get; set; }
    public DbSet<RepoRoot> RepoRoots { get; set; }
    public DbSet<InviteCode> InviteCodes { get; set; }
    public DbSet<InviteCodeUse> InviteCodeUses { get; set; }
    //public DbSet<EmailToken> EmailTokens { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}