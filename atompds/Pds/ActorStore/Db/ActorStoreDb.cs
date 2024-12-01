using atompds.Pds.ActorStore.Db.Schema;
using Microsoft.EntityFrameworkCore;

namespace atompds.Pds.ActorStore.Db;

public class ActorStoreDb : DbContext
{
    // migration compat
    private ActorStoreDb() : base(new DbContextOptionsBuilder<ActorStoreDb>()
        .UseSqlite("Data Source=stubactorstore.db").Options)
    {
    }
    
    public ActorStoreDb(DbContextOptions<ActorStoreDb> options) : base(options)
    {
    }
    
    public DbSet<AccountPref> AccountPrefs { get; set; }
    public DbSet<RepoRoot> RepoRoots { get; set; }
    public DbSet<Backlink> Backlinks { get; set; }
    public DbSet<Blob> Blobs { get; set; }
    public DbSet<RecordBlob> RecordBlobs { get; set; }
    public DbSet<Record> Records { get; set; }
    public DbSet<RepoBlock> RepoBlocks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // repo_root_pkey = {did}
        modelBuilder.Entity<RepoRoot>().HasKey(rr => rr.Did);
        
        // repo_block_pkey = {cid}
        modelBuilder.Entity<RepoBlock>().HasKey(rb => rb.Cid);
        
        // repo_block_repo_rev_idx = {repoRev, cid}
        modelBuilder.Entity<RepoBlock>().HasIndex(rb => new { rb.RepoRev, rb.Cid });
        
        // record_pkey = {uri}
        modelBuilder.Entity<Record>().HasKey(r => r.Uri);
        
        // record_cid_idx = {cid}
        modelBuilder.Entity<Record>().HasIndex(r => r.Cid);
        
        // record_collection_idx = {collection}
        modelBuilder.Entity<Record>().HasIndex(r => r.Collection);
        
        // record_repo_rev_idx = {repoRev}
        modelBuilder.Entity<Record>().HasIndex(r => r.RepoRev);
        
        // blob_pkey = {cid}
        modelBuilder.Entity<Blob>().HasKey(b => b.Cid);
        
        // blob_tempKey_idx = {tempKey}
        modelBuilder.Entity<Blob>().HasIndex(b => b.TempKey);
        
        // record_blob_pkey = {blobCid, recordUri}
        modelBuilder.Entity<RecordBlob>().HasKey(rb => new { rb.BlobCid, rb.RecordUri });
        
        // backlink_pkey = {uri, path}
        modelBuilder.Entity<Backlink>().HasKey(bl => new { bl.Uri, bl.Path });
        
        // backlink_link_to_idx = {path, linkTo}
        modelBuilder.Entity<Backlink>().HasIndex(bl => new { bl.Path, bl.LinkTo });
        
        // account_pref_pkey = {id}
        modelBuilder.Entity<AccountPref>().HasKey(ap => ap.Id);
    }
}