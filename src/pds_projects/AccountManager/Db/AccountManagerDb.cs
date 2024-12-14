using Microsoft.EntityFrameworkCore;

namespace AccountManager.Db;

public class AccountManagerDb : DbContext
{
    public AccountManagerDb() : base(new DbContextOptionsBuilder<AccountManagerDb>()
        .UseSqlite("Data Source=stubaccountmanager.db").Options)
    {
        
    }
    
    public AccountManagerDb(DbContextOptions<AccountManagerDb> options) : base(options)
    {
    }
    
    public DbSet<Actor> Actors { get; set; }
    public DbSet<Account> Accounts { get; set; }
    //public DbSet<Device> Devices { get; set; }
    //public DbSet<DeviceAccount> DeviceAccounts { get; set; }
    //public DbSet<AuthorizationRequest> AuthorizationRequests { get; set; }
    //public DbSet<Token> Tokens { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    //public DbSet<UsedRefreshToken> UsedRefreshTokens { get; set; }
    public DbSet<AppPassword> AppPasswords { get; set; }
    public DbSet<RepoRoot> RepoRoots { get; set; }
    public DbSet<InviteCode> InviteCodes { get; set; }
    public DbSet<InviteCodeUse> InviteCodeUses { get; set; }
    public DbSet<EmailToken> EmailTokens { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // app_password_pkey = {did, name}
        modelBuilder.Entity<AppPassword>().HasKey(ap => new { ap.Did, ap.Name });
        
        // invite_code_pkey = {code}
        modelBuilder.Entity<InviteCode>().HasKey(ic => ic.Code);
        
        // invite_code_for_account_idx = {for_account}
        modelBuilder.Entity<InviteCode>().HasIndex(ic => ic.ForAccount);
        
        // invite_code_use_pkey = {code, used_by}
        modelBuilder.Entity<InviteCodeUse>().HasKey(icu => new { icu.Code, icu.UsedBy });
        
        // refresh_token_pkey = {id}
        modelBuilder.Entity<RefreshToken>().HasKey(rt => rt.Id);
        
        // refresh_token_did_idx = {did}
        modelBuilder.Entity<RefreshToken>().HasIndex(rt => rt.Did);
        
        // repo_root_pkey = {did}
        modelBuilder.Entity<RepoRoot>().HasKey(rr => rr.Did);
        
        // actor_pkey = {did}
        modelBuilder.Entity<Actor>().HasKey(a => a.Did);
        
        // actor_handle_lower_idx = {handle_lower} <- cannot use this using fluent mappings
        // modelBuilder.Entity<Actor>().HasIndex(a => a.Handle.ToLower());
        
        // actor_cursor_idx = {created_ad, did}
        modelBuilder.Entity<Actor>().HasIndex(a => new { a.CreatedAt, a.Did });
        
        // account_pkey = {did}
        modelBuilder.Entity<Account>().HasKey(a => a.Did);
        
        // actor <-> account 1:1 optional
        modelBuilder.Entity<Actor>().HasOne(a => a.Account)
            .WithOne(a => a.Actor)
            .HasForeignKey<Account>(a => a.Did)
            .IsRequired(false);
        
        // account_email_lower_idx = {email_lower} <- cannot use this using fluent mappings
        // modelBuilder.Entity<Account>().HasIndex(a => a.Email.ToLower());
        
        // email_token_pkey = {purpose, did}
        modelBuilder.Entity<EmailToken>().HasKey(et => new { et.Purpose, et.Did });
        // unique constraint, {purpose, token}
        modelBuilder.Entity<EmailToken>().HasIndex(et => new { et.Purpose, et.Token }).IsUnique();
    }
}