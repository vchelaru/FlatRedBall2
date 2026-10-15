using System;
using System.Collections.Generic;
using System.Numerics;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Adapts a <see cref="ShapeSave"/> to <see cref="IKeyframeShape"/> for editor-side evaluation. Reads through
/// to the save object, so it always reflects the shape's current values; curve names are parsed once into
/// <see cref="KeyframeCurve"/>. A rectangle's size is its <c>ScaleX</c>/<c>ScaleY</c>, a circle's its <c>Radius</c>.
/// </summary>
public sealed class SaveKeyframeShape : IKeyframeShape
{
    private SaveKeyframeShape(ShapeSave source)
    {
        Source = source;
        OffsetCurve = KeyframeCurve.Parse(source.OffsetInterpolation, source.OffsetEasing);
        SizeCurve = source is SizedShapeSave sized ? KeyframeCurve.Parse(sized.SizeInterpolation, sized.SizeEasing) : KeyframeCurve.Snap;
        PointsCurve = source is PolygonSave poly ? KeyframeCurve.Parse(poly.PointsInterpolation, poly.PointsEasing) : KeyframeCurve.Snap;
    }

    /// <summary>Adapts <paramref name="shape"/>.</summary>
    public static SaveKeyframeShape Create(ShapeSave shape) => new(shape);

    /// <summary>The geometry family of <paramref name="shape"/>.</summary>
    public static KeyframeShapeKind KindOf(ShapeSave shape) => shape switch
    {
        AARectSave => KeyframeShapeKind.Rectangle,
        CircleSave => KeyframeShapeKind.Circle,
        _ => KeyframeShapeKind.Polygon,
    };

    /// <summary>The save object this adapter reads.</summary>
    public ShapeSave Source { get; }

    /// <inheritdoc/>
    public string Name => Source.Name;

    /// <inheritdoc/>
    public KeyframeShapeKind Kind => KindOf(Source);

    /// <inheritdoc/>
    public float X => Source.X;

    /// <inheritdoc/>
    public float Y => Source.Y;

    /// <inheritdoc/>
    public float SizeX => Source switch { AARectSave r => r.ScaleX, CircleSave c => c.Radius, _ => 0f };

    /// <inheritdoc/>
    public float SizeY => Source is AARectSave r ? r.ScaleY : 0f;

    /// <inheritdoc/>
    public bool SkipOffsetKeyframe => Source.SkipOffsetKeyframe;

    /// <inheritdoc/>
    public bool SkipSizeKeyframe => Source is SizedShapeSave sized && sized.SkipSizeKeyframe;

    /// <inheritdoc/>
    public KeyframeCurve OffsetCurve { get; }

    /// <inheritdoc/>
    public KeyframeCurve SizeCurve { get; }

    /// <inheritdoc/>
    public IReadOnlyList<Vector2> Points
    {
        get
        {
            if (Source is not PolygonSave poly) return Array.Empty<Vector2>();
            var points = new Vector2[poly.Points.Count];
            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2(poly.Points[i].X, poly.Points[i].Y);
            return points;
        }
    }

    /// <inheritdoc/>
    public bool SkipPointsKeyframe => Source is PolygonSave poly && poly.SkipPointsKeyframe;

    /// <inheritdoc/>
    public KeyframeCurve PointsCurve { get; }
}
