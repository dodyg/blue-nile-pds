namespace Sequencer.Types;

public class TypedAccountEvt : ISeqEvt
{
    public TypedCommitType Type => TypedCommitType.Account;
    public required int Seq { get; init; }
    public required DateTime Time { get; init; }
    public required AccountEvt Evt { get; init; }
}