using CID;

namespace Repo;

public class RepoException : Exception
{
    public RepoException(string message) : base(message) { }
    public RepoException(string message, Exception innerException) : base(message, innerException) { }
}

public class MissingBlockException : RepoException
{
    public Cid Cid { get; }

    public MissingBlockException(Cid cid, string source)
        : base($"Block {cid} not found{(string.IsNullOrEmpty(source) ? "" : $" (source: {source})")}")
    {
        Cid = cid;
        base.Source = source;
    }
}

public class MissingBlocksException : RepoException
{
    public Cid[] Cids { get; }

    public MissingBlocksException(Cid[] cids, string source)
        : base($"Missing blocks: {string.Join(", ", cids)}{(string.IsNullOrEmpty(source) ? "" : $" (source: {source})")}")
    {
        Cids = cids;
        base.Source = source;
    }
}

public class MissingCommitBlocksException : RepoException
{
    public Cid CommitCid { get; }
    public Cid[] Missing { get; }

    public MissingCommitBlocksException(Cid commitCid, Cid[] missing)
        : base($"Missing commit blocks for {commitCid}: {string.Join(", ", missing)}")
    {
        CommitCid = commitCid;
        Missing = missing;
    }
}

public class UnexpectedObjectException : RepoException
{
    public Cid Cid { get; }
    public string ExpectedType { get; }
    public string ActualType { get; }

    public UnexpectedObjectException(Cid cid, string expectedType, string actualType)
        : base($"Unexpected object at {cid}: expected {expectedType}, got {actualType}")
    {
        Cid = cid;
        ExpectedType = expectedType;
        ActualType = actualType;
    }
}
