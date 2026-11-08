using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// One placement of an animation chain on a <see cref="SequenceTrackSave"/>. The next chain on the track can cut it
/// short; see <see cref="SequenceTimeline"/>.
/// </summary>
public class SequenceChainReferenceSave
{
    /// <summary>
    /// The <c>.achx</c>/<c>.achj</c> holding the chain, as written in <see cref="SequenceListSave.Sources"/>. Kept as
    /// written even when that file is missing.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>The chain's name inside <see cref="Source"/>. Kept as written even when no such chain exists.</summary>
    public string Chain { get; set; } = "";

    /// <summary>Seconds from the sequence start.</summary>
    public double Start { get; set; }

    /// <summary>
    /// The block's length in playback cycles. One cycle is the chain's full length, twice that for a ping-pong chain. It
    /// does not override the chain's own Loop flag: a chain with Loop on repeats across the length (a fraction plays part
    /// of the last cycle), and one with Loop off plays one cycle, then holds its last frame until the length ends. The
    /// JSON key stays <c>loops</c>.
    /// </summary>
    public float Loops { get; set; } = 1;

    /// <summary>
    /// Properties this code does not know, kept as raw JSON and written back unchanged after the known ones. See
    /// <see cref="SequenceListSave.UnknownProperties"/>.
    /// </summary>
    public JsonObject UnknownProperties { get; } = new();
}
