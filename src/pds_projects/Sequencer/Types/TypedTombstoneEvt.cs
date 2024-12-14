namespace Sequencer.Types;

public class TypedTombstoneEvt : ISeqEvt
{
    public required TombstoneEvt Evt { get; init; }
    public TypedCommitType Type => TypedCommitType.Tombstone;
    public required int Seq { get; init; }
    public required DateTime Time { get; init; }
}