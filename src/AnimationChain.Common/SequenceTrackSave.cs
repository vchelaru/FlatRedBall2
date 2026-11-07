using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// One sprite's lane in a <see cref="SequenceSave"/>. Its offset, flip and draw order hold for the whole sequence.
/// Outside every chain on the track, the sprite is hidden.
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
    /// Draw order: higher draws on top. Equal values draw in track order, a later track on top. The editor lists tracks
    /// in file order, top lane first, so the bottom lane is in front; dragging a lane header rewrites the list order and
    /// moves this value only as far as keeping that true takes.
    /// </summary>
    public int Z { get; set; }

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
