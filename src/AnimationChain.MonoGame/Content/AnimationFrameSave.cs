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

    /// <summary>
    /// User-visible display label. Only meaningful when <see cref="HasCustomName"/> is <c>true</c>;
    /// otherwise the editor shows a dynamic position-based label ("Frame N").
    /// </summary>
    public string Name = string.Empty;

    /// <summary>When <c>true</c>, <see cref="Name"/> was explicitly set by the user.</summary>
    public bool HasCustomName;

    /// <summary>Per-frame shape definitions. <c>null</c> when no shapes are defined.</summary>
    public ShapesSave? ShapesSave;
}
