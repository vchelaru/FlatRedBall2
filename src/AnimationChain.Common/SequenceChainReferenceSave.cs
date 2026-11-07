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
    /// How many playback cycles play, overriding the chain's own Loop flag. One cycle is the chain's full length, twice
    /// that for a ping-pong chain. A fraction plays part of the last cycle.
    /// </summary>
    public float Loops { get; set; } = 1;
}
