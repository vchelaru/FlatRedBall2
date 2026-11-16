using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A track that plays another sequence of the same <c>.seqj</c> instead of chains: the target's tracks are inlined under
/// this track's name. It is a clip like a chain placement, with a <see cref="Start"/> and a length in <see cref="Loops"/>
/// cycles of the target, but it never extends the sequence that holds it (see <see cref="SequenceTimeline.Length"/>).
/// The target restarts whenever the holding sequence starts, so switching sequences restarts it. Cycles are not allowed
/// (see <see cref="SequenceReferences"/>).
/// </summary>
public class SequenceReferenceSave
{
    /// <summary>
    /// The <see cref="SequenceSave.Name"/> of the sequence to play. Kept as written even when no such sequence exists; a
    /// missing target plays nothing.
    /// </summary>
    public string Sequence { get; set; } = "";

    /// <summary>Seconds from the start of the holding sequence.</summary>
    public double Start { get; set; }

    /// <summary>
    /// The clip's length in cycles of the target's length (<see cref="SequenceTimeline.Length"/>). A target with
    /// <see cref="SequenceSave.Loop"/> on repeats across it; one with Loop off plays once, then holds its final pose until
    /// the clip ends. The JSON key is <c>loops</c>, like a chain's.
    /// </summary>
    public float Loops { get; set; } = 1;

    /// <summary>
    /// Properties this code does not know, kept as raw JSON and written back unchanged after the known ones. See
    /// <see cref="SequenceListSave.UnknownProperties"/>.
    /// </summary>
    public JsonObject UnknownProperties { get; } = new();
}
