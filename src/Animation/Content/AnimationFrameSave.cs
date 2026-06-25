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
    /// Optional per-frame red channel, 0–255. <c>null</c> (the default) means unset, so it is
    /// omitted from the saved <c>.achx</c>. These values are <b>not</b> applied by the engine or
    /// the Animation Editor — game code reads them via <see cref="FlatRedBall2.Rendering.Sprite.CurrentFrame"/>
    /// and decides how to use them (tint, flash, etc.). See <see cref="Green"/>, <see cref="Blue"/>.
    /// </summary>
    public int? Red;

    /// <summary>Optional per-frame green channel, 0–255. See <see cref="Red"/> for the game-consumed contract.</summary>
    public int? Green;

    /// <summary>Optional per-frame blue channel, 0–255. See <see cref="Red"/> for the game-consumed contract.</summary>
    public int? Blue;

    /// <summary>
    /// Optional per-frame color operation describing how <see cref="Red"/>/<see cref="Green"/>/<see cref="Blue"/>
    /// combine with the texture. <c>null</c> (the default) means none and is omitted from the saved
    /// <c>.achx</c>. Like the channels, runtimes interpret this as they choose. See <see cref="FlatRedBall2.Animation.ColorOperation"/>.
    /// </summary>
    public ColorOperation? ColorOperation;

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
