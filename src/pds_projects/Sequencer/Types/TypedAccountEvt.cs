namespace Sequencer.Types;

public class TypedAccountEvt : ISeqEvt
{
    public required AccountEvt Evt { get; init; }
    public TypedCommitType Type => TypedCommitType.Account;
    public required int Seq { get; init; }
    public required DateTime Time { get; init; }
}