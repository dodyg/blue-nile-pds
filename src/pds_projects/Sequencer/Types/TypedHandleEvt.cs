namespace Sequencer.Types;

public class TypedHandleEvt : ISeqEvt
{
    public required HandleEvt Evt { get; init; }
    public TypedCommitType Type => TypedCommitType.Handle;
    public required int Seq { get; init; }
    public required DateTime Time { get; init; }
}