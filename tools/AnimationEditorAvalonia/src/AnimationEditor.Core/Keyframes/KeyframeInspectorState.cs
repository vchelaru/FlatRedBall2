using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Keyframes;

/// <summary>
/// What the inspector shows for one keyframe group (diamond, curve icons, field state) given the
/// selected frames. <see cref="IsKeyframe"/> is <c>null</c> when the selection disagrees.
/// </summary>
/// <param name="IsKeyframe">All keyframes (<c>true</c>), none (<c>false</c>), or mixed (<c>null</c>).</param>
/// <param name="CanToggle">False when every selected frame is the first of its chain, which is always a keyframe.</param>
/// <param name="Curve">The picker icon to highlight; <c>null</c> for no highlight (mixed, or outside the eight).</param>
/// <param name="CurveNote">Tooltip text when <paramref name="Curve"/> is <c>null</c>: the stored curve's name, or "Mixed".</param>
public sealed record KeyframeGroupState(bool? IsKeyframe, bool CanToggle, KeyframeCurveChoice? Curve, string? CurveNote)
{
    /// <summary>The curve icons show unless every selected frame has the keyframe off.</summary>
    public bool CurvesVisible => IsKeyframe != false;

    /// <summary>The group's value fields take edits unless every selected frame has the keyframe off.</summary>
    public bool FieldsEditable => IsKeyframe != false;
}

/// <summary>Reads the keyframe diamond and curve state of a frame selection (#1478).</summary>
public static class KeyframeInspectorState
{
    /// <summary>
    /// Computes the state for <paramref name="group"/>. A chain's first frame counts as a keyframe whatever
    /// its stored flag says, and a frame that is not a keyframe has its curve ignored here.
    /// </summary>
    public static KeyframeGroupState For(
        KeyframeGroup group, IReadOnlyList<AnimationFrameSave> frames,
        Func<AnimationFrameSave, AnimationChainSave?> chainOf)
    {
        int keyframes = 0, others = 0;
        var curves = new List<(string? Type, string? Easing)>();
        foreach (var frame in frames)
        {
            if (IsEffectivelyKeyframe(group, frame, chainOf(frame)))
            {
                keyframes++;
                curves.Add(GetCurve(group, frame));
            }
            else
            {
                others++;
            }
        }

        bool canToggle = frames.Any(f => !IsFirstFrame(f, chainOf(f)));
        bool? isKeyframe = others == 0 ? true : keyframes == 0 ? false : null;

        var (curve, note) = SummarizeCurves(curves);
        return new KeyframeGroupState(isKeyframe, canToggle, curve, note);
    }

    /// <summary>Whether <paramref name="frame"/> is a keyframe of <paramref name="group"/>: the first frame always, else unless its skip flag is set.</summary>
    public static bool IsEffectivelyKeyframe(KeyframeGroup group, AnimationFrameSave frame, AnimationChainSave? chain) =>
        IsFirstFrame(frame, chain) || !GetSkip(group, frame);

    /// <summary>Whether <paramref name="frame"/> is the first frame of <paramref name="chain"/>.</summary>
    public static bool IsFirstFrame(AnimationFrameSave frame, AnimationChainSave? chain) =>
        chain is not null && chain.Frames.Count > 0 && ReferenceEquals(chain.Frames[0], frame);

    /// <summary>
    /// The offset the inspector shows for <paramref name="frame"/>: its own when it is a keyframe, else the
    /// value in effect at the start of the frame (the glide from the previous keyframe), so a read-only
    /// field reads what playback shows there.
    /// </summary>
    public static (float X, float Y) DisplayOffset(AnimationFrameSave frame, AnimationChainSave? chain)
    {
        if (chain is null || IsEffectivelyKeyframe(KeyframeGroup.Offset, frame, chain))
            return (frame.RelativeX, frame.RelativeY);
        var sample = KeyframeSampler.Sample(chain, chain.Frames.IndexOf(frame), 0);
        return (sample.OffsetX, sample.OffsetY);
    }

    /// <summary>
    /// The color channels the inspector shows for <paramref name="frame"/>: its own stored channels (so an
    /// omitted one stays blank) when it is a keyframe, else the value in effect at the start of the frame.
    /// </summary>
    public static KeyframeColor DisplayColor(AnimationFrameSave frame, AnimationChainSave? chain)
    {
        if (chain is null || IsEffectivelyKeyframe(KeyframeGroup.Color, frame, chain))
            return new KeyframeColor(frame.Red, frame.Green, frame.Blue, frame.Alpha);
        var color = KeyframeSampler.Sample(chain, chain.Frames.IndexOf(frame), 0).Color;
        return new KeyframeColor(color.Red, color.Green, color.Blue, color.Alpha);
    }

    /// <summary>
    /// Computes the state for a shape group. A shape that is the first of a run of frames holding its name always
    /// counts as a keyframe whatever its flag says, and a shape that is not a keyframe has its curve ignored here.
    /// A group a shape does not have (a polygon's size, a rectangle's points) reads as an always-on keyframe with the snap curve.
    /// </summary>
    public static KeyframeGroupState ForShapes(
        ShapeKeyframeGroup group, IReadOnlyList<ShapeSave> shapes, Func<ShapeSave, AnimationChainSave?> chainOf)
    {
        int keyframes = 0, others = 0;
        var curves = new List<(string? Type, string? Easing)>();
        foreach (var shape in shapes)
        {
            if (IsEffectivelyKeyframe(group, shape, chainOf(shape)))
            {
                keyframes++;
                curves.Add(GetCurve(group, shape));
            }
            else
            {
                others++;
            }
        }

        bool canToggle = shapes.Any(s => HasGroup(group, s) && !IsRunStart(s, chainOf(s)));
        bool? isKeyframe = others == 0 ? true : keyframes == 0 ? false : null;
        var (curve, note) = SummarizeCurves(curves);
        return new KeyframeGroupState(isKeyframe, canToggle, curve, note);
    }

    /// <summary>Whether <paramref name="shape"/> has the keyframe group (a polygon has no size; only a polygon has points).</summary>
    public static bool HasGroup(ShapeKeyframeGroup group, ShapeSave shape) => group switch
    {
        ShapeKeyframeGroup.Offset => true,
        ShapeKeyframeGroup.Size => shape is SizedShapeSave,
        _ => shape is PolygonSave,
    };

    /// <summary>Whether <paramref name="shape"/> is a keyframe of <paramref name="group"/>: the first of its run always, else unless its skip flag is set.</summary>
    public static bool IsEffectivelyKeyframe(ShapeKeyframeGroup group, ShapeSave shape, AnimationChainSave? chain) =>
        !HasGroup(group, shape) || IsRunStart(shape, chain) || !GetSkip(group, shape);

    /// <summary>Whether <paramref name="shape"/> is the first of a run of frames holding its name (see <see cref="KeyframeEvaluator.IsShapeRunStart"/>).</summary>
    public static bool IsRunStart(ShapeSave shape, AnimationChainSave? chain)
    {
        int index = IndexOfFrameHolding(shape, chain);
        return chain is null || index < 0 || KeyframeEvaluator.IsShapeRunStart(chain, index, shape);
    }

    /// <summary>
    /// The offset the inspector shows for <paramref name="shape"/>: its own when it is a keyframe, else the value in
    /// effect at the start of its frame, so a read-only field reads what playback shows there.
    /// </summary>
    public static (float X, float Y) DisplayShapeOffset(ShapeSave shape, AnimationChainSave? chain)
    {
        if (chain is null || IsEffectivelyKeyframe(ShapeKeyframeGroup.Offset, shape, chain))
            return (shape.X, shape.Y);
        var value = KeyframeSampler.SampleShape(chain, IndexOfFrameHolding(shape, chain), 0, shape);
        return (value.X, value.Y);
    }

    /// <summary>
    /// The size components the inspector shows for <paramref name="shape"/> (rectangle ScaleX/ScaleY, circle Radius and 0):
    /// its own when it is a keyframe, else the value in effect at the start of its frame.
    /// </summary>
    public static (float SizeX, float SizeY) DisplayShapeSize(ShapeSave shape, AnimationChainSave? chain)
    {
        if (chain is null || IsEffectivelyKeyframe(ShapeKeyframeGroup.Size, shape, chain))
        {
            var own = SaveKeyframeShape.Create(shape);
            return (own.SizeX, own.SizeY);
        }
        var value = KeyframeSampler.SampleShape(chain, IndexOfFrameHolding(shape, chain), 0, shape);
        return (value.SizeX, value.SizeY);
    }

    /// <summary>
    /// The points the inspector shows for <paramref name="polygon"/>: its own when it is a points keyframe, else the
    /// value in effect at the start of its frame (the glide from the previous points keyframe).
    /// </summary>
    public static IReadOnlyList<System.Numerics.Vector2> DisplayShapePoints(PolygonSave polygon, AnimationChainSave? chain)
    {
        if (chain is null || IsEffectivelyKeyframe(ShapeKeyframeGroup.Points, polygon, chain))
            return SaveKeyframeShape.Create(polygon).Points;
        return KeyframeSampler.SampleShapePoints(chain, IndexOfFrameHolding(polygon, chain), 0, polygon);
    }

    internal static int IndexOfFrameHolding(ShapeSave shape, AnimationChainSave? chain)
    {
        if (chain is null) return -1;
        for (int i = 0; i < chain.Frames.Count; i++)
            if (chain.Frames[i].ShapesSave?.Shapes.Contains(shape) == true) return i;
        return -1;
    }

    internal static bool GetSkip(ShapeKeyframeGroup group, ShapeSave shape) => group switch
    {
        ShapeKeyframeGroup.Offset => shape.SkipOffsetKeyframe,
        ShapeKeyframeGroup.Size => shape is SizedShapeSave sized && sized.SkipSizeKeyframe,
        _ => shape is PolygonSave poly && poly.SkipPointsKeyframe,
    };

    internal static void SetSkip(ShapeKeyframeGroup group, ShapeSave shape, bool skip)
    {
        if (group == ShapeKeyframeGroup.Offset) shape.SkipOffsetKeyframe = skip;
        else if (group == ShapeKeyframeGroup.Size && shape is SizedShapeSave sized) sized.SkipSizeKeyframe = skip;
        else if (group == ShapeKeyframeGroup.Points && shape is PolygonSave poly) poly.SkipPointsKeyframe = skip;
    }

    internal static (string? Type, string? Easing) GetCurve(ShapeKeyframeGroup group, ShapeSave shape) => group switch
    {
        ShapeKeyframeGroup.Offset => (shape.OffsetInterpolation, shape.OffsetEasing),
        ShapeKeyframeGroup.Size => shape is SizedShapeSave sized ? (sized.SizeInterpolation, sized.SizeEasing) : (null, null),
        _ => shape is PolygonSave poly ? (poly.PointsInterpolation, poly.PointsEasing) : (null, null),
    };

    internal static void SetCurve(ShapeKeyframeGroup group, ShapeSave shape, string? type, string? easing)
    {
        if (group == ShapeKeyframeGroup.Offset)
        {
            shape.OffsetInterpolation = type;
            shape.OffsetEasing = easing;
        }
        else if (group == ShapeKeyframeGroup.Size && shape is SizedShapeSave sized)
        {
            sized.SizeInterpolation = type;
            sized.SizeEasing = easing;
        }
        else if (group == ShapeKeyframeGroup.Points && shape is PolygonSave poly)
        {
            poly.PointsInterpolation = type;
            poly.PointsEasing = easing;
        }
    }

    private static (KeyframeCurveChoice? Curve, string? Note) SummarizeCurves(List<(string? Type, string? Easing)> curves)
    {
        if (curves.Count == 0) return (null, null);
        var first = curves[0];
        bool allSame = curves.All(c => c.Type == first.Type && c.Easing == first.Easing);
        var choices = curves.Select(c => KeyframeCurvePicker.FromStored(c.Type, c.Easing)).Distinct().ToList();
        if (choices.Count == 1 && choices[0] is { } single) return (single, null);
        return (null, allSame ? KeyframeCurvePicker.DescribeStored(first.Type, first.Easing) : "Mixed");
    }

    internal static bool GetSkip(KeyframeGroup group, AnimationFrameSave frame) =>
        group == KeyframeGroup.Offset ? frame.SkipOffsetKeyframe : frame.SkipColorKeyframe;

    internal static void SetSkip(KeyframeGroup group, AnimationFrameSave frame, bool skip)
    {
        if (group == KeyframeGroup.Offset) frame.SkipOffsetKeyframe = skip;
        else frame.SkipColorKeyframe = skip;
    }

    internal static (string? Type, string? Easing) GetCurve(KeyframeGroup group, AnimationFrameSave frame) =>
        group == KeyframeGroup.Offset
            ? (frame.OffsetInterpolation, frame.OffsetEasing)
            : (frame.ColorInterpolation, frame.ColorEasing);

    internal static void SetCurve(KeyframeGroup group, AnimationFrameSave frame, string? type, string? easing)
    {
        if (group == KeyframeGroup.Offset)
        {
            frame.OffsetInterpolation = type;
            frame.OffsetEasing = easing;
        }
        else
        {
            frame.ColorInterpolation = type;
            frame.ColorEasing = easing;
        }
    }
}
