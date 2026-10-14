using FlatRedBall2.Animation;

namespace FlatRedBall2.AnimationEditorCommon;

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
    /// Optional per-frame red channel, -255..255 (negative values subtract under
    /// <see cref="FlatRedBall2.Animation.ColorOperation.Add"/>). <c>null</c> (the default) is omitted from the
    /// saved <c>.achx</c> and inherits the most recent earlier frame in the same chain that sets it
    /// (see <see cref="EffectiveFrameColor"/>). FlatRedBall2's <c>Sprite.Draw</c> applies the resolved
    /// value per <see cref="ColorOperation"/>. See <see cref="Green"/>, <see cref="Blue"/>.
    /// </summary>
    public int? Red;

    /// <summary>Optional per-frame green channel, -255..255. See <see cref="Red"/>.</summary>
    public int? Green;

    /// <summary>Optional per-frame blue channel, -255..255. See <see cref="Red"/>.</summary>
    public int? Blue;

    /// <summary>
    /// Optional per-frame alpha (straight transparency), 0–255, independent of <see cref="ColorOperation"/>.
    /// <c>null</c> inherits the same way as <see cref="Red"/>. FlatRedBall2's <c>Sprite.Draw</c> multiplies
    /// the resolved value into the sprite's alpha.
    /// </summary>
    public int? Alpha;

    /// <summary>
    /// Optional per-frame color operation describing how <see cref="Red"/>/<see cref="Green"/>/<see cref="Blue"/>
    /// combine with the texture. <c>null</c> inherits the same way as <see cref="Red"/>; if no frame in the
    /// chain sets it, R/G/B are not applied. See <see cref="FlatRedBall2.Animation.ColorOperation"/>.
    /// </summary>
    public ColorOperation? ColorOperation;

    /// <summary>
    /// When <c>true</c>, this frame is not an <b>offset keyframe</b>: its own <see cref="RelativeX"/>/
    /// <see cref="RelativeY"/> and <see cref="OffsetInterpolation"/> are ignored, and the previous offset
    /// keyframe's curve glides across this frame to the next offset keyframe over the summed
    /// <see cref="FrameLength"/>s. The first frame is always a keyframe regardless. Written only when
    /// <c>true</c>. See <see cref="KeyframeEvaluator"/>.
    /// </summary>
    public bool SkipOffsetKeyframe;

    /// <summary>
    /// When <c>true</c>, this frame is not a <b>color keyframe</b>: its own <see cref="Red"/>/<see cref="Green"/>/
    /// <see cref="Blue"/>/<see cref="Alpha"/> and <see cref="ColorInterpolation"/> are ignored (nothing
    /// inherits from them either; <see cref="ColorOperation"/> still applies, as it never interpolates).
    /// The first frame is always a keyframe. Written only when <c>true</c>. Independent of
    /// <see cref="SkipOffsetKeyframe"/>.
    /// </summary>
    public bool SkipColorKeyframe;

    /// <summary>
    /// How the offset gets from this keyframe to the next: an <c>InterpolationType</c> name from
    /// <c>FlatRedBall.InterpolationCore</c> (<c>Linear</c>, <c>Quadratic</c>, <c>Cubic</c>, <c>Back</c>,
    /// <c>Bounce</c>, <c>Elastic</c>, ...). <c>null</c> (omitted from the file) or <c>Instant</c> means snap,
    /// today's behavior. Stored as a raw string so values this version does not understand survive a
    /// round trip; the format promises nothing about what a runtime does with it, and the recommendation for
    /// a name a runtime does not know is to treat it as snap. See <see cref="KeyframeCurve.Parse"/>.
    /// </summary>
    public string? OffsetInterpolation;

    /// <summary>
    /// <c>In</c>, <c>Out</c> or <c>InOut</c> for <see cref="OffsetInterpolation"/>; <c>null</c> (omitted) means
    /// <c>Out</c>. Ignored for snap and <c>Linear</c>.
    /// </summary>
    public string? OffsetEasing;

    /// <summary>The curve from this keyframe's color to the next color keyframe's. Same rules as <see cref="OffsetInterpolation"/>.</summary>
    public string? ColorInterpolation;

    /// <summary>Easing for <see cref="ColorInterpolation"/>. Same rules as <see cref="OffsetEasing"/>.</summary>
    public string? ColorEasing;

    /// <summary>Per-frame shape definitions. Empty by default.</summary>
    public ShapesSave? ShapesSave;

    /// <summary>
    /// Named events raised when playback enters this frame. Saved only when non-empty, so files
    /// without events stay byte-identical. An FRB2 extension: Gum and FRB1 ignore these and drop
    /// them if they re-save the file.
    /// </summary>
    public List<AnimationFrameEvent> Events { get; } = new();
}
