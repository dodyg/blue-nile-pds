using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sequencer.Db;

public class SequencerDb : DbContext
{
    private SequencerDb() : base(new DbContextOptionsBuilder<SequencerDb>()
        .UseSqlite("Data Source=stubsequencer.db").Options)
    {
    }

    public SequencerDb(DbContextOptions<SequencerDb> options) : base(options)
    {
    }

    public DbSet<RepoSeq> RepoSeqs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RepoSeq>()
            .Property(x => x.EventType)
            .HasConversion<string>();

        modelBuilder.Entity<RepoSeq>()
            .Property(x => x.Seq)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<RepoSeq>()
            .Property(x => x.SequencedAt)
            .HasConversion(
                v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}

public class RepoSeq
{
    [Key] public int Seq { get; set; }

    public required string Did { get; set; }
    public required RepoSeqEventType EventType { get; set; }
    public required byte[] Event { get; set; }
    public bool Invalidated { get; set; }
    public required DateTime SequencedAt { get; set; }
}

public enum RepoSeqEventType
{
    Append,
    Rebase,
    Handle,
    Migrate,
    Identity,
    Account,
    Tombstone
}