namespace Sequencer.Types;

public interface ISeqEvt
{
    public TypedCommitType Type { get; }
    public int Seq { get; }
    public DateTime Time { get; }
}