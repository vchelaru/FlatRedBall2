using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A named timeline in a <c>.seqj</c>, played by name. It has no stored length: it ends when its last chain stops
/// playing (<see cref="SequenceTimeline.Length"/>).
/// </summary>
public class SequenceSave
{
    /// <summary>The name game code plays the sequence by.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Whether playback restarts at 0 once the sequence ends. On by default, like <see cref="AnimationChainSave.Loop"/>;
    /// a file with no <c>loop</c> key reads as on. Off, the sequence stops at its end and holds its final pose (the
    /// composite of its last instant; a track whose chains already finished stays hidden).
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>The sprites, one per track.</summary>
    public List<SequenceTrackSave> Tracks { get; set; } = new();

    /// <summary>
    /// Properties this code does not know, kept as raw JSON and written back unchanged after the known ones. See
    /// <see cref="SequenceListSave.UnknownProperties"/>.
    /// </summary>
    public JsonObject UnknownProperties { get; } = new();
}
