namespace Sequencer.Types;

public class TypedCommitEvt : ISeqEvt
{
    public TypedCommitType Type => TypedCommitType.Commit;
    public required int Seq { get; init; }
    public required DateTime Time { get; init; }
    public required CommitEvt Evt { get; init; }
}