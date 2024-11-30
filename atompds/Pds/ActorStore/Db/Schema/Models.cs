using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace atompds.Pds.ActorStore.Db.Schema;

[Table(TableName)]
public class AccountPref
{
    public const string TableName = "account_pref";
    
    [Key]
    public int Id { get; set; }
    
    [StringLength(255)]
    public required string Name { get; set; }
    
    [StringLength(255)]
    public required string ValueJson { get; set; }
}

[Table(TableName)]
public class RepoRoot
{
    public const string TableName = "repo_root";
    
    public required string Did { get; set; }
    public required string Cid { get; set; }
    public required string Rev { get; set; }
    public required DateTime IndexedAt { get; set; }
}

[Table(TableName)]
public class Backlink
{
    public const string TableName = "backlink";
    
    public required string Uri { get; set; }
    public required string Path { get; set; }
    public required string LinkTo { get; set; }
}

[Table(TableName)]
public class Blob
{
    public const string TableName = "blob";
    
    public required string Cid { get; set; }
    public required string MimeType { get; set; }
    public required int Size { get; set; }
    public string? TempKey { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public required DateTime CreatedAt { get; set; }
    public string? TakedownRef { get; set; }
}

[Table(TableName)]
public class RecordBlob
{
    public const string TableName = "record_blob";
    
    public required string BlobCid { get; set; }
    public required string RecordUri { get; set; }
}

[Table(TableName)]
public class Record
{
    public const string TableName = "record";
    
    public required string Uri { get; set; }
    public required string Cid { get; set; }
    public required string Collection { get; set; }
    public required string Rkey { get; set; }
    public required string RepoRev { get; set; }
    public required DateTime IndexedAt { get; set; }
    public string? TakedownRef { get; set; }
}

[Table(TableName)]
public class RepoBlock
{
    public const string TableName = "repo_block";
    
    public required string Cid { get; set; }
    public required string RepoRev { get; set; }
    public required int Size { get; set; }
    public required byte[] Content { get; set; }
}

