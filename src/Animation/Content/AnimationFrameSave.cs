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

    /// <summary>
    /// User-visible display label for this frame in the Animation Editor tree.
    /// Only meaningful when <see cref="HasCustomName"/> is <c>true</c>; when
    /// <c>false</c> the editor shows a dynamic position-based label ("Frame N")
    /// that updates automatically on reorder.
    /// </summary>
    public string Name = string.Empty;

    /// <summary>When <c>true</c>, <see cref="Name"/> was explicitly set by the user and
    /// the editor displays it as-is. When <c>false</c> (the default), the editor shows
    /// a dynamic position-based label ("Frame N") that updates automatically on reorder.</summary>
    public bool HasCustomName;

    /// <summary>Per-frame shape definitions. Empty by default.</summary>
    public ShapesSave? ShapesSave;
}
