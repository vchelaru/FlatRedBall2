namespace FlatRedBall.AnimationChain.Content;

/// <summary>
/// Deserialized representation of a single frame within an <see cref="AnimationChainSave"/>.
/// </summary>
public class AnimationFrameSave
{
    /// <summary>Name of the texture file for this frame.</summary>
    public string TextureName = string.Empty;

    /// <summary>Frame display time. Units depend on <see cref="AnimationChainListSave.TimeMeasurementUnit"/>.</summary>
    public float FrameLength;

    /// <summary>Left texture coordinate. UV (0–1) or pixel, depending on <see cref="AnimationChainListSave.CoordinateType"/>.</summary>
    public float LeftCoordinate;

    /// <summary>Right texture coordinate.</summary>
    public float RightCoordinate = 1f;

    /// <summary>Top texture coordinate.</summary>
    public float TopCoordinate;

    /// <summary>Bottom texture coordinate.</summary>
    public float BottomCoordinate = 1f;

    /// <summary>Whether the texture should be flipped horizontally.</summary>
    public bool FlipHorizontal;

    /// <summary>Whether the texture should be flipped vertically.</summary>
    public bool FlipVertical;

    /// <summary>Per-frame X offset.</summary>
    public float RelativeX;

    /// <summary>Per-frame Y offset.</summary>
    public float RelativeY;

    /// <summary>Per-frame shape definitions. <c>null</c> when no shapes are defined.</summary>
    public ShapesSave? ShapesSave;
}
