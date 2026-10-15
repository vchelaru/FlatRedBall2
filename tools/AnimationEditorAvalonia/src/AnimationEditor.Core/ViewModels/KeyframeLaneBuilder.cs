using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.ViewModels;

/// <summary>One keyframe-to-next-keyframe stretch of a lane, in the timeline strip's content pixels.</summary>
/// <param name="Curve">The starting keyframe's curve, the one the evaluator uses for the whole stretch.</param>
public readonly record struct KeyframeLaneSpan(double StartX, double EndX, KeyframeCurve Curve);

/// <summary>A stretch where a keyframe's value is held with nothing after it to glide to (drawn as a flat solid line).</summary>
public readonly record struct KeyframeLaneHold(double StartX, double EndX);

/// <summary>
/// What one keyframe lane (a Sprite's Offset or Color, a shape's Offset or Size) draws under the timeline
/// cells (#1478, #1489): everything is in the strip's content pixels, so the cells' own widths are the only scale.
/// </summary>
/// <param name="KeyframeXs">The left edge of every keyframe's frame.</param>
/// <param name="Spans">Keyframe to next keyframe; a looping chain's last keyframe runs to the end of the strip.</param>
/// <param name="Holds">Where the last keyframe of a run holds with nothing to glide to: to the end of the strip for a non-looping chain, to the end of its run for a shape's. Empty when it glides back to the first keyframe instead.</param>
/// <param name="TickXs">The right edge of every cell that has lane content (a shape lane leaves none for a frame without the shape).</param>
public sealed record KeyframeLane(
    IReadOnlyList<double> KeyframeXs,
    IReadOnlyList<KeyframeLaneSpan> Spans,
    IReadOnlyList<KeyframeLaneHold> Holds,
    double ContentWidth,
    IReadOnlyList<double> TickXs);

/// <summary>Computes the keyframe lanes shown under the single-chain timeline strip (#1478, #1489).</summary>
public static class KeyframeLaneBuilder
{
    /// <summary>Height of one lane in pixels.</summary>
    public const double LaneHeight = 16.0;

    /// <summary>Half the width of a keyframe diamond in pixels; the first one spills this far left of the strip.</summary>
    public const double DiamondHalfWidth = 4.5;

    /// <summary>
    /// The leftmost lane x (in the lane control's own coordinates) that may draw. At scroll offset 0 the
    /// first keyframe's diamond sits on the strip's left edge and spills past it, so the left half is kept;
    /// once the strip has scrolled, anything left of the viewport is clipped as with the cells.
    /// </summary>
    public static double ClipLeft(double scrollOffset) => scrollOffset > 0 ? 0 : -DiamondHalfWidth;

    /// <summary>
    /// Builds the lane for <paramref name="group"/>, or <c>null</c> when it should not show: no chain, fewer
    /// than two keyframes, or the property is not in use. Every frame is a keyframe until it opts out, so
    /// a default chain has one per frame, all holding the same value; its lane would only repeat the cells.
    /// The lane shows when some keyframe's effective value differs from the previous keyframe's, or some
    /// span glides (a non-snap curve). Once it shows, every keyframe gets a diamond, including those that
    /// repeat the previous value. A frame is a keyframe by the same rule the evaluator and the inspector use
    /// (<see cref="KeyframeInspectorState.IsEffectivelyKeyframe"/>), and color values are resolved through
    /// <see cref="EffectiveFrameColor"/>. <paramref name="cellWidths"/> are the strip's cell widths, one
    /// per frame; a span's x range is the sum of the widths it covers.
    /// </summary>
    public static KeyframeLane? Build(AnimationChainSave? chain, KeyframeGroup group, IReadOnlyList<double> cellWidths)
    {
        if (chain is null || chain.Frames.Count == 0 || cellWidths.Count < chain.Frames.Count)
            return null;

        int count = chain.Frames.Count;
        var edges = new double[count + 1];
        for (int i = 0; i < count; i++) edges[i + 1] = edges[i] + cellWidths[i];

        var keyframes = new List<int>();
        for (int i = 0; i < count; i++)
        {
            if (KeyframeInspectorState.IsEffectivelyKeyframe(group, chain.Frames[i], chain))
                keyframes.Add(i);
        }
        if (keyframes.Count < 2) return null;

        var spans = new List<KeyframeLaneSpan>();
        for (int k = 0; k < keyframes.Count; k++)
        {
            bool isLast = k == keyframes.Count - 1;
            if (isLast && !chain.Loop) break;
            int from = keyframes[k];
            double endX = isLast ? edges[count] : edges[keyframes[k + 1]];
            var (type, easing) = KeyframeInspectorState.GetCurve(group, chain.Frames[from]);
            spans.Add(new KeyframeLaneSpan(edges[from], endX, KeyframeCurve.Parse(type, easing)));
        }

        bool inUse = spans.Any(sp => !sp.Curve.IsSnap) || ValuesDiffer(chain, group, keyframes);
        if (!inUse) return null;

        double lastX = edges[keyframes[^1]];
        return new KeyframeLane(
            keyframes.Select(i => edges[i]).ToList(),
            spans,
            chain.Loop ? Array.Empty<KeyframeLaneHold>() : new[] { new KeyframeLaneHold(lastX, edges[count]) },
            edges[count],
            edges.Skip(1).ToList());
    }

    /// <summary>
    /// Builds the lane of <paramref name="shape"/>'s <paramref name="group"/> across the chain, or <c>null</c> when
    /// it should not show: no chain, an unnamed or ambiguously named shape (it snaps), a group the shape lacks (a
    /// polygon's size), or the group not in use. The shape's name and kind are its identity: every frame that holds
    /// exactly one shape of that name and kind has lane content, found with <see cref="KeyframeEvaluator.TryMatch"/>
    /// like playback. A frame without it leaves a gap that no span or hold crosses. The first frame of each run of
    /// such frames is always a keyframe; later ones are unless their skip flag is set, and a skipped frame has no
    /// diamond (the span from the previous keyframe runs through it). A run's last keyframe holds to the end of its
    /// run, except on a looping chain whose run ends the chain while frame 0 holds the name, which glides to frame 0
    /// across the loop point. Shows when, within a run, some keyframe's value differs from the previous keyframe's or
    /// some span glides. <paramref name="cellWidths"/> are the strip's cell widths, one per frame.
    /// </summary>
    public static KeyframeLane? BuildShape(
        AnimationChainSave? chain, ShapeSave? shape, ShapeKeyframeGroup group, IReadOnlyList<double> cellWidths)
    {
        if (chain is null || shape is null || chain.Frames.Count == 0 || cellWidths.Count < chain.Frames.Count) return null;
        if (!KeyframeInspectorState.HasGroup(group, shape)) return null;

        string name = shape.Name;
        var kind = SaveKeyframeShape.KindOf(shape);
        var frames = SaveKeyframeFrame.Create(chain);
        int count = frames.Length;

        var match = new IKeyframeShape?[count];
        for (int i = 0; i < count; i++)
            match[i] = KeyframeEvaluator.TryMatch(frames[i], name, kind, out var m) ? m : null;
        // The selected shape must itself be matchable (named, and not ambiguous in its own frame), else it snaps.
        int own = KeyframeInspectorState.IndexOfFrameHolding(shape, chain);
        if (own < 0 || match[own] is null) return null;

        var edges = new double[count + 1];
        for (int i = 0; i < count; i++) edges[i + 1] = edges[i] + cellWidths[i];

        var keyframeXs = new List<double>();
        var spans = new List<KeyframeLaneSpan>();
        var holds = new List<KeyframeLaneHold>();
        var ticks = new List<double>();
        bool valuesDiffer = false;

        for (int runStart = 0; runStart < count; runStart++)
        {
            if (match[runStart] is null) continue;
            int runEnd = runStart;
            while (runEnd + 1 < count && match[runEnd + 1] is not null) runEnd++;

            var keyframes = new List<int> { runStart };
            for (int i = runStart + 1; i <= runEnd; i++)
                if (!IsSkipped(group, match[i]!)) keyframes.Add(i);

            for (int k = 0; k < keyframes.Count; k++)
            {
                int from = keyframes[k];
                keyframeXs.Add(edges[from]);
                bool isLast = k == keyframes.Count - 1;
                if (!isLast)
                {
                    spans.Add(new KeyframeLaneSpan(edges[from], edges[keyframes[k + 1]], CurveOf(group, match[from]!)));
                    valuesDiffer |= ValuesDiffer(group, match[from]!, match[keyframes[k + 1]]!);
                }
                else if (runEnd == count - 1 && chain.Loop && match[0] is not null && from != 0)
                {
                    spans.Add(new KeyframeLaneSpan(edges[from], edges[count], CurveOf(group, match[from]!)));
                }
                else
                {
                    holds.Add(new KeyframeLaneHold(edges[from], edges[runEnd + 1]));
                }
            }

            for (int i = runStart; i <= runEnd; i++) ticks.Add(edges[i + 1]);
            runStart = runEnd;
        }

        bool inUse = valuesDiffer || spans.Any(sp => !sp.Curve.IsSnap);
        return inUse ? new KeyframeLane(keyframeXs, spans, holds, edges[count], ticks) : null;
    }

    private static bool IsSkipped(ShapeKeyframeGroup group, IKeyframeShape shape) =>
        group == ShapeKeyframeGroup.Offset ? shape.SkipOffsetKeyframe : shape.SkipSizeKeyframe;

    private static KeyframeCurve CurveOf(ShapeKeyframeGroup group, IKeyframeShape shape) =>
        group == ShapeKeyframeGroup.Offset ? shape.OffsetCurve : shape.SizeCurve;

    private static bool ValuesDiffer(ShapeKeyframeGroup group, IKeyframeShape a, IKeyframeShape b) =>
        group == ShapeKeyframeGroup.Offset
            ? a.X != b.X || a.Y != b.Y
            : a.SizeX != b.SizeX || a.SizeY != b.SizeY;

    // Whether any keyframe's value differs from the previous keyframe's, compared on effective values.
    private static bool ValuesDiffer(AnimationChainSave chain, KeyframeGroup group, IReadOnlyList<int> keyframes)
    {
        if (group == KeyframeGroup.Offset)
        {
            for (int k = 1; k < keyframes.Count; k++)
            {
                var prev = chain.Frames[keyframes[k - 1]];
                var next = chain.Frames[keyframes[k]];
                if (prev.RelativeX != next.RelativeX || prev.RelativeY != next.RelativeY) return true;
            }
            return false;
        }

        var colors = EffectiveFrameColor.ResolveAll(chain.Frames);
        for (int k = 1; k < keyframes.Count; k++)
        {
            if (!SameColor(colors[keyframes[k - 1]], colors[keyframes[k]])) return true;
        }
        return false;
    }

    // An unset channel counts as the operation's identity (255, or 0 for Add), like the evaluator.
    private static bool SameColor(ResolvedFrameColor a, ResolvedFrameColor b)
    {
        int idA = EffectiveFrameColor.ChannelDefault(a.Operation), idB = EffectiveFrameColor.ChannelDefault(b.Operation);
        return (a.Red ?? idA) == (b.Red ?? idB)
            && (a.Green ?? idA) == (b.Green ?? idB)
            && (a.Blue ?? idA) == (b.Blue ?? idB)
            && (a.Alpha ?? 255) == (b.Alpha ?? 255);
    }

    /// <summary>
    /// The polyline of <paramref name="curve"/> over one span: <c>T</c> is 0..1 across the span's width and
    /// <c>Progress</c> the eased value (0 at the left keyframe, 1 at the next; Back and Bounce leave 0..1
    /// in between). Snap is flat, then jumps at the end, rather than sampled. Uses the shared
    /// <see cref="KeyframeCurve.Evaluate"/>, so it is exactly what playback does.
    /// </summary>
    public static IReadOnlyList<(double T, double Progress)> CurvePoints(KeyframeCurve curve, int steps)
    {
        if (curve.IsSnap) return new (double, double)[] { (0, 0), (1, 0), (1, 1) };

        steps = Math.Max(1, steps);
        var points = new (double, double)[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            points[i] = (t, curve.Evaluate((float)t));
        }
        return points;
    }
}
