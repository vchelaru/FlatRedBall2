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

    /// <summary>Draw order: higher draws on top. Equal values draw in track order.</summary>
    public int Z { get; set; }

    /// <summary>
    /// Chain references in any order. Playback orders them by <see cref="SequenceChainReferenceSave.Start"/>, and list
    /// order breaks ties (see <see cref="SequenceTimeline"/>).
    /// </summary>
    public List<SequenceChainReferenceSave> Chains { get; set; } = new();
}
