using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// One sprite's lane in a <see cref="SequenceSave"/>. Its offset and flip hold for the whole sequence. Outside every
/// chain on the track, the sprite is hidden. A track has no depth value: the tracks of a sequence stack in list order, the
/// first in front and the last at the back, like layers in an image editor (the editor shows them as lanes, the top lane in front).
/// </summary>
public class SequenceTrackSave
{
    /// <summary>Display name of the sprite.</summary>
    public string Name { get; set; } = "";

    /// <summary>Offset from the entity origin, Y+ up like a frame's own offsets.</summary>
    public float X { get; set; }

    /// <inheritdoc cref="X"/>
    public float Y { get; set; }

    /// <summary>Mirrors the sprite and its frame offsets across the track's X.</summary>
    public bool FlipHorizontal { get; set; }

    /// <summary>
    /// Initial visibility of the track. The editor preview hides the track when false and runtimes may skip drawing
    /// it; game code may still enable it by name at runtime. Written to the file only when false, so every file
    /// without hidden tracks stays unchanged.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Chain references in any order. Playback orders them by <see cref="SequenceChainReferenceSave.Start"/>, and list
    /// order breaks ties (see <see cref="SequenceTimeline"/>).
    /// </summary>
    public List<SequenceChainReferenceSave> Chains { get; set; } = new();

    /// <summary>
    /// Properties this code does not know, kept as raw JSON and written back unchanged after the known ones. See
    /// <see cref="SequenceListSave.UnknownProperties"/>.
    /// </summary>
    public JsonObject UnknownProperties { get; } = new();
}
