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

    /// <summary>
    /// Whether the texture region should be transposed (swap X/Y) — a third mirror axis independent
    /// of <see cref="FlipHorizontal"/>/<see cref="FlipVertical"/>, matching Tiled's diagonal tile-flip
    /// flag. Combining all three yields all 8 orientations of a source region.
    /// </summary>
    public bool FlipDiagonal;

    /// <summary>The frame's offset along the X axis.</summary>
    public float RelativeX;

    /// <summary>The frame's offset along the Y axis.</summary>
    public float RelativeY;

    /// <summary>
    /// Optional per-frame red channel, 0–255. <c>null</c> (the default) means unset, so it is
    /// omitted from the saved <c>.achx</c>. The Animation Editor previews these as a reference tint
    /// (via <see cref="ColorOperation"/>); the FlatRedBall2 runtime does <b>not</b> auto-apply them —
    /// game code reads them via <c>FlatRedBall2.Rendering.Sprite.CurrentFrame</c> and decides how
    /// to use them (tint, flash, etc.). See <see cref="Green"/>, <see cref="Blue"/>.
    /// </summary>
    public int? Red;

    /// <summary>Optional per-frame green channel, 0–255. See <see cref="Red"/> for the game-consumed contract.</summary>
    public int? Green;

    /// <summary>Optional per-frame blue channel, 0–255. See <see cref="Red"/> for the game-consumed contract.</summary>
    public int? Blue;

    /// <summary>
    /// Optional per-frame alpha (transparency) channel, 0–255. <c>null</c> (the default) means unset, so it
    /// is omitted from the saved <c>.achx</c>. Straight transparency, independent of <see cref="ColorOperation"/>.
    /// The Animation Editor previews it as opacity (a reference render); the FlatRedBall2 runtime does <b>not</b>
    /// auto-apply it — game code reads it via <c>FlatRedBall2.Rendering.Sprite.CurrentFrame</c>. See <see cref="Red"/>.
    /// </summary>
    public int? Alpha;

    /// <summary>
    /// Optional per-frame color operation describing how <see cref="Red"/>/<see cref="Green"/>/<see cref="Blue"/>
    /// combine with the texture. <c>null</c> (the default) means none and is omitted from the saved
    /// <c>.achx</c>. Like the channels, runtimes interpret this as they choose. See <see cref="FlatRedBall2.Animation.ColorOperation"/>.
    /// </summary>
    public ColorOperation? ColorOperation;

    /// <summary>Per-frame shape definitions. Empty by default.</summary>
    public ShapesSave? ShapesSave;
}
