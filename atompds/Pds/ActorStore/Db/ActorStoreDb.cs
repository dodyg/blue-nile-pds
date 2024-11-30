using atompds.Pds.ActorStore.Db.Schema;
using Microsoft.EntityFrameworkCore;

namespace atompds.Pds.ActorStore.Db;

public class ActorStoreDb : DbContext
{
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
}