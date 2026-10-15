using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.ViewModels;

/// <summary>One keyframe-to-next-keyframe stretch of a lane, in the timeline strip's content pixels.</summary>
/// <param name="Curve">The starting keyframe's curve, the one the evaluator uses for the whole stretch.</param>
public readonly record struct KeyframeLaneSpan(double StartX, double EndX, KeyframeCurve Curve);

/// <summary>
/// What one keyframe lane (Offset or Color) draws under the timeline cells (#1478): everything is in the
/// strip's content pixels, so the cells' own widths are the only scale.
/// </summary>
/// <param name="KeyframeXs">The left edge of every keyframe's frame.</param>
/// <param name="Spans">Keyframe to next keyframe; a looping chain's last keyframe runs to the end of the strip.</param>
/// <param name="HoldFromX">Where the last keyframe starts holding to the end of the strip, or <c>null</c> for a looping chain, which glides back to the first keyframe instead.</param>
/// <param name="TickXs">The right edge of every cell.</param>
public sealed record KeyframeLane(
    KeyframeGroup Group,
    IReadOnlyList<double> KeyframeXs,
    IReadOnlyList<KeyframeLaneSpan> Spans,
    double? HoldFromX,
    double ContentWidth,
    IReadOnlyList<double> TickXs);

/// <summary>Computes the keyframe lanes shown under the single-chain timeline strip (#1478).</summary>
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
            group,
            keyframes.Select(i => edges[i]).ToList(),
            spans,
            chain.Loop ? null : lastX,
            edges[count],
            edges.Skip(1).ToList());
    }

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
