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

    /// <summary>Whether the texture should be flipped diagonally (transposed).</summary>
    public bool FlipDiagonal;

    /// <summary>Per-frame X offset.</summary>
    public float RelativeX;

    /// <summary>Per-frame Y offset.</summary>
    public float RelativeY;

    /// <summary>Per-frame red color channel (0-255). <c>null</c> means unset/not authored.</summary>
    public int? Red;

    /// <summary>Per-frame green color channel (0-255). <c>null</c> means unset/not authored.</summary>
    public int? Green;

    /// <summary>Per-frame blue color channel (0-255). <c>null</c> means unset/not authored.</summary>
    public int? Blue;

    /// <summary>Per-frame alpha/transparency channel (0-255). <c>null</c> means unset/not authored.</summary>
    public int? Alpha;

    /// <summary>How the color channels combine with the texture. <c>null</c> means none.</summary>
    public ColorOperation? ColorOperation;

    /// <summary>Per-frame shape definitions. <c>null</c> when no shapes are defined.</summary>
    public ShapesSave? ShapesSave;
}
