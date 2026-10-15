using System.Collections.Generic;
using System.Numerics;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A per-frame shape definition carried by <see cref="AnimationFrameBase.Shapes"/>. Exposed as
/// plain data — the caller (e.g. an entity's animation-driven shape reconciliation) is
/// responsible for applying these values to real collision shapes. Read the glided values through
/// <see cref="KeyframeEvaluator.EvaluateShape{TFrame}"/>; a renderer that ignores the curves keeps snapping.
/// </summary>
public abstract class AnimationShapeFrame : IKeyframeShape
{
    /// <summary>
    /// Identifier matched against named shapes on the entity/sprite. Required and non-empty —
    /// unnamed entries are rejected when the file is loaded.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Local X offset from the owning entity. Applied each frame switch.</summary>
    public float RelativeX { get; set; }

    /// <summary>Local Y offset from the owning entity. Applied each frame switch.</summary>
    public float RelativeY { get; set; }

    /// <summary>When <c>true</c> this shape is not an offset keyframe (see <see cref="ShapeSave.SkipOffsetKeyframe"/>).</summary>
    public bool SkipOffsetKeyframe { get; set; }

    /// <summary>The curve from this shape's offset to the next offset keyframe's. Default is snap.</summary>
    public KeyframeCurve OffsetCurve { get; set; }

    /// <summary>When <c>true</c> this shape is not a size keyframe (see <see cref="SizedShapeSave.SkipSizeKeyframe"/>).</summary>
    public bool SkipSizeKeyframe { get; set; }

    /// <summary>The curve from this shape's size to the next size keyframe's. Default is snap.</summary>
    public KeyframeCurve SizeCurve { get; set; }

    /// <summary>The geometry family, used to match shapes across frames.</summary>
    protected abstract KeyframeShapeKind ShapeKind { get; }

    /// <summary>First size component in the shape's own unit (see <see cref="IKeyframeShape.SizeX"/>).</summary>
    protected virtual float SizeXValue => 0f;

    /// <summary>Second size component in the shape's own unit (see <see cref="IKeyframeShape.SizeY"/>).</summary>
    protected virtual float SizeYValue => 0f;

    /// <summary>The polygon points as the evaluator sees them; empty for every other kind.</summary>
    protected virtual IReadOnlyList<Vector2> PointsValue => System.Array.Empty<Vector2>();

    /// <summary>The curve from a polygon's points to the next points keyframe's; snap for every other kind.</summary>
    protected virtual KeyframeCurve PointsCurveValue => default;

    /// <summary>Whether a polygon is not a points keyframe; always <c>false</c> for every other kind.</summary>
    protected virtual bool SkipPointsKeyframeValue => false;

    KeyframeShapeKind IKeyframeShape.Kind => ShapeKind;
    IReadOnlyList<Vector2> IKeyframeShape.Points => PointsValue;
    bool IKeyframeShape.SkipPointsKeyframe => SkipPointsKeyframeValue;
    KeyframeCurve IKeyframeShape.PointsCurve => PointsCurveValue;
    float IKeyframeShape.X => RelativeX;
    float IKeyframeShape.Y => RelativeY;
    float IKeyframeShape.SizeX => SizeXValue;
    float IKeyframeShape.SizeY => SizeYValue;
}

/// <summary>Per-frame definition for an axis-aligned rectangle shape.</summary>
public class AnimationAARectFrame : AnimationShapeFrame
{
    /// <summary>Width in world units.</summary>
    public float Width { get; set; } = 32f;

    /// <summary>Height in world units.</summary>
    public float Height { get; set; } = 32f;

    /// <inheritdoc/>
    protected override KeyframeShapeKind ShapeKind => KeyframeShapeKind.Rectangle;

    /// <inheritdoc/>
    protected override float SizeXValue => Width;

    /// <inheritdoc/>
    protected override float SizeYValue => Height;
}

/// <summary>Per-frame definition for a circle shape.</summary>
public class AnimationCircleFrame : AnimationShapeFrame
{
    /// <summary>Circle radius in world units.</summary>
    public float Radius { get; set; } = 16f;

    /// <inheritdoc/>
    protected override KeyframeShapeKind ShapeKind => KeyframeShapeKind.Circle;

    /// <inheritdoc/>
    protected override float SizeXValue => Radius;
}

/// <summary>Per-frame definition for a polygon shape.</summary>
public class AnimationPolygonFrame : AnimationShapeFrame
{
    /// <summary>Polygon vertices in the shape's local space.</summary>
    public Vector2[] Points { get; set; } = System.Array.Empty<Vector2>();

    /// <summary>When <c>true</c> this polygon is not a points keyframe (see <see cref="PolygonSave.SkipPointsKeyframe"/>).</summary>
    public bool SkipPointsKeyframe { get; set; }

    /// <summary>The curve from this polygon's points to the next points keyframe's. Default is snap.</summary>
    public KeyframeCurve PointsCurve { get; set; }

    /// <inheritdoc/>
    protected override IReadOnlyList<Vector2> PointsValue => Points;

    /// <inheritdoc/>
    protected override KeyframeCurve PointsCurveValue => PointsCurve;

    /// <inheritdoc/>
    protected override bool SkipPointsKeyframeValue => SkipPointsKeyframe;

    /// <inheritdoc/>
    protected override KeyframeShapeKind ShapeKind => KeyframeShapeKind.Polygon;
}
