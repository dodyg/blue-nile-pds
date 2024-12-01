namespace Repo;

public class Class1
{

}

public record CommitData(string Cid, string Rev, DateTime? Since, string? Prev, /*BlockMap NewBlocks,*/ HashSet<string> RemovedCids);