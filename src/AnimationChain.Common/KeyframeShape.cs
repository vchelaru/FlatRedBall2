namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>The geometry family of a per-frame shape. Shapes only interpolate into a same-named shape of the same kind.</summary>
public enum KeyframeShapeKind
{
    /// <summary>An axis-aligned rectangle; size is its width/height (or half-extents).</summary>
    Rectangle,

    /// <summary>A circle; size is its radius.</summary>
    Circle,

    /// <summary>A polygon; only its offset interpolates (its points are not interpolated).</summary>
    Polygon,
}

/// <summary>
/// One per-frame shape as the keyframe evaluator sees it (#1478). Matched across frames by
/// <see cref="Name"/> and <see cref="Kind"/>. Implemented by the engine's and the generic runtime's
/// shape frames and by the editor's save-shape adapter.
/// </summary>
public interface IKeyframeShape
{
    /// <summary>Shape name. Empty or duplicated within its frame means the shape cannot be matched and snaps.</summary>
    string Name { get; }

    /// <summary>Geometry family; a name that changes kind between frames does not glide.</summary>
    KeyframeShapeKind Kind { get; }

    /// <summary>The shape's own X offset. Ignored when <see cref="SkipOffsetKeyframe"/> is set.</summary>
    float X { get; }

    /// <summary>The shape's own Y offset. Ignored when <see cref="SkipOffsetKeyframe"/> is set.</summary>
    float Y { get; }

    /// <summary>
    /// First size component in the implementer's own unit: a rectangle's width (or half-width), a circle's
    /// radius. Zero for a polygon. Interpolation is linear in the stored value, so the unit does not matter.
    /// </summary>
    float SizeX { get; }

    /// <summary>Second size component: a rectangle's height (or half-height). Zero for a circle or polygon.</summary>
    float SizeY { get; }

    /// <summary>When <c>true</c> the shape is not an offset keyframe: its own offset and curve are ignored.</summary>
    bool SkipOffsetKeyframe { get; }

    /// <summary>When <c>true</c> the shape is not a size keyframe: its own size and curve are ignored.</summary>
    bool SkipSizeKeyframe { get; }

    /// <summary>The curve from this shape's offset to the next offset keyframe's.</summary>
    KeyframeCurve OffsetCurve { get; }

    /// <summary>The curve from this shape's size to the next size keyframe's.</summary>
    KeyframeCurve SizeCurve { get; }
}

/// <summary>A shape's offset and size at one instant of playback, in the same units as <see cref="IKeyframeShape"/>.</summary>
public readonly record struct KeyframeShapeValue(float X, float Y, float SizeX, float SizeY);
