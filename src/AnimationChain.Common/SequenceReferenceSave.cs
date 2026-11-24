using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// One clip of a reference track (<see cref="SequenceTrackSave.References"/>): it plays another sequence of the same
/// <c>.seqj</c>, whose tracks are inlined under the track's name. A clip is like a chain placement, with a
/// <see cref="Start"/> and a <see cref="Length"/> (one run of the target unless set), and the next clip on the track cuts it
/// short exactly like the next chain cuts a chain (see <see cref="SequenceTimeline"/>). Like a chain it extends the sequence
/// that holds it to its end (see <see cref="SequenceTimeline.Length"/>).
/// The target restarts at the clip's start and whenever the holding sequence starts, so switching sequences restarts it. A reference whose target
/// plays the holder back (a cycle) is kept but plays nothing (see <see cref="SequenceReferences.Plays"/>).
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
    /// The clip's length in seconds, or null for one run of the target at its current length
    /// (<see cref="SequenceTimeline.Length"/>), so the clip follows edits to the target. A target with
    /// <see cref="SequenceSave.Loop"/> on repeats across it; one with Loop off plays once, then holds its final pose until
    /// the clip ends. JSON key <c>length</c>, omitted when null, like a chain's.
    /// </summary>
    public double? Length { get; set; }

    /// <summary>
    /// Properties this code does not know, kept as raw JSON and written back unchanged after the known ones. See
    /// <see cref="SequenceListSave.UnknownProperties"/>.
    /// </summary>
    public JsonObject UnknownProperties { get; } = new();
}
