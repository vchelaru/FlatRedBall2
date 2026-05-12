namespace FlatRedBall2.Animation.Content;

/// <summary>
/// Deserialized representation of a single frame within an <see cref="AnimationChainSave"/>.
/// </summary>
public class AnimationFrameSave
{
    /// <summary>Name of the texture to load for this frame.</summary>
    public string TextureName = string.Empty;

    /// <summary>Frame display time. Units depend on <see cref="AnimationChainListSave.TimeMeasurementUnit"/>.</summary>
    public float FrameLength;

    /// <summary>Left texture coordinate. UV (0–1) or pixel, depending on <see cref="AnimationChainListSave.CoordinateType"/>.</summary>
    public float LeftCoordinate;

    /// <summary>Right texture coordinate. UV (0–1) or pixel.</summary>
    public float RightCoordinate = 1f;

    /// <summary>Top texture coordinate. UV (0–1) or pixel.</summary>
    public float TopCoordinate;

    /// <summary>Bottom texture coordinate. UV (0–1) or pixel.</summary>
    public float BottomCoordinate = 1f;

    /// <summary>Whether the texture should be flipped horizontally.</summary>
    public bool FlipHorizontal;

    /// <summary>Whether the texture should be flipped vertically.</summary>
    public bool FlipVertical;

    /// <summary>The frame's offset along the X axis.</summary>
    public float RelativeX;

    /// <summary>The frame's offset along the Y axis.</summary>
    public float RelativeY;

    /// <summary>Per-frame shape definitions. Empty by default.</summary>
    public ShapesSave? ShapesSave;
}
