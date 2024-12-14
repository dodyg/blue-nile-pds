namespace Sequencer.Types;

public class TypedHandleEvt : ISeqEvt
{
    public TypedCommitType Type => TypedCommitType.Handle;
    public required int Seq { get; init; }
    public required DateTime Time { get; init; }
    public required HandleEvt Evt { get; init; }
}