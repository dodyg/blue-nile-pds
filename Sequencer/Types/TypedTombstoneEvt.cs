namespace Sequencer.Types;

public class TypedTombstoneEvt : ISeqEvt
{
    public TypedCommitType Type => TypedCommitType.Tombstone;
    public required int Seq { get; init; }
    public required DateTime Time { get; init; }
    public required TomestoneEvt Evt { get; init; }
}