using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.ViewModels;

/// <summary>One keyframe-to-next-keyframe stretch of a lane, in the timeline strip's content pixels.</summary>
/// <param name="Curve">The earlier keyframe's curve, the one the evaluator uses for the whole stretch in either direction.</param>
/// <param name="Backward">Played right to left (#1538): the value leaves the right keyframe and arrives at the left one.
/// The curve's shape over x is the same as forward (the backward glide retraces it); only snap differs, holding the right value.</param>
/// <param name="TStart">Where in the curve (0..1, left keyframe to right) this piece starts; a backward glide across the
/// loop point is split into two pieces at the strip's ends.</param>
/// <param name="TEnd">Where in the curve this piece ends.</param>
public readonly record struct KeyframeLaneSpan(double StartX, double EndX, KeyframeCurve Curve,
    bool Backward = false, double TStart = 0, double TEnd = 1);

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
    public static KeyframeLane? Build(AnimationChainSave? chain, KeyframeGroup group, IReadOnlyList<double> cellWidths,
        bool backward = false)
    {
        if (chain is null || chain.Frames.Count == 0 || cellWidths.Count < chain.Frames.Count)
            return null;

        int count = chain.Frames.Count;
        var edges = new double[count + 1];
        for (int i = 0; i < count; i++) edges[i + 1] = edges[i] + cellWidths[i];
        bool wraps = PlaybackOrder.GlidesAcrossLoop(chain.Loop, chain.PingPong);

        var keyframes = new List<int>();
        for (int i = 0; i < count; i++)
        {
            if (KeyframeInspectorState.IsEffectivelyKeyframe(group, chain.Frames[i], chain))
                keyframes.Add(i);
        }
        if (keyframes.Count < 2) return null;

        KeyframeCurve CurveAt(int frame)
        {
            var (type, easing) = KeyframeInspectorState.GetCurve(group, chain.Frames[frame]);
            return KeyframeCurve.Parse(type, easing);
        }

        var spans = new List<KeyframeLaneSpan>();
        var holds = new List<KeyframeLaneHold>();
        IReadOnlyList<double> keyframeXs;
        if (!backward)
        {
            for (int k = 0; k < keyframes.Count; k++)
            {
                bool isLast = k == keyframes.Count - 1;
                if (isLast && !wraps) break;
                int from = keyframes[k];
                double endX = isLast ? edges[count] : edges[keyframes[k + 1]];
                spans.Add(new KeyframeLaneSpan(edges[from], endX, CurveAt(from)));
            }
            if (!wraps) holds.Add(new KeyframeLaneHold(edges[keyframes[^1]], edges[count]));
            keyframeXs = keyframes.Select(i => edges[i]).ToList();
        }
        else
        {
            // Showing frame N glides back to the previous keyframe, arriving when that frame starts: the span between
            // keyframes J < K covers cells J+1..K, and K's value is reached at its cell's right edge.
            for (int k = 1; k < keyframes.Count; k++)
                spans.Add(new KeyframeLaneSpan(edges[keyframes[k - 1] + 1], edges[keyframes[k] + 1], CurveAt(keyframes[k - 1]), Backward: true));
            int last = keyframes[^1];
            if (wraps)
                AddBackwardWrap(spans, edges, count, last, CurveAt(last));
            else
            {
                holds.Add(new KeyframeLaneHold(edges[0], edges[1]));
                // Cells after the last keyframe show it, as they do going forward.
                if (last < count - 1) holds.Add(new KeyframeLaneHold(edges[last + 1], edges[count]));
            }
            keyframeXs = keyframes.Select(i => edges[i + 1]).ToList();
        }

        bool inUse = spans.Any(sp => !sp.Curve.IsSnap) || ValuesDiffer(chain, group, keyframes);
        if (!inUse) return null;

        return new KeyframeLane(keyframeXs, spans, holds, edges[count], edges.Skip(1).ToList());
    }

    // The backward glide from frame 0 across the loop point to the last keyframe L shows cell 0, then cells L+1..end:
    // one curve split into two pieces, the strip's right end (L+1..end, from L's side) and its left cell (frame 0).
    private static void AddBackwardWrap(List<KeyframeLaneSpan> spans, double[] edges, int count, int last, KeyframeCurve curve)
    {
        double first = edges[1] - edges[0];
        double tail = edges[count] - edges[last + 1];
        double split = first + tail > 0 ? tail / (first + tail) : 0;
        if (tail > 0)
            spans.Add(new KeyframeLaneSpan(edges[last + 1], edges[count], curve, Backward: true, TStart: 0, TEnd: split));
        spans.Add(new KeyframeLaneSpan(edges[0], edges[1], curve, Backward: true, TStart: split, TEnd: 1));
    }

    /// <summary>
    /// Builds the lane of <paramref name="shape"/>'s <paramref name="group"/> across the chain, or <c>null</c> when
    /// it should not show: no chain, an unnamed or ambiguously named shape (it snaps), a group the shape lacks (a
    /// polygon's size, a rectangle's or circle's points), or the group not in use. The shape's name and kind are its identity: every frame that holds
    /// exactly one shape of that name and kind has lane content, found with <see cref="KeyframeEvaluator.TryMatch"/>
    /// like playback. A frame without it leaves a gap that no span or hold crosses. The first frame of each run of
    /// such frames is always a keyframe; later ones are unless their skip flag is set, and a skipped frame has no
    /// diamond (the span from the previous keyframe runs through it). A run's last keyframe holds to the end of its
    /// run, except on a looping chain whose run ends the chain while frame 0 holds the name, which glides to frame 0
    /// across the loop point. Shows when, within a run, some keyframe's value differs from the previous keyframe's or
    /// some span glides. <paramref name="cellWidths"/> are the strip's cell widths, one per frame.
    /// </summary>
    public static KeyframeLane? BuildShape(
        AnimationChainSave? chain, ShapeSave? shape, ShapeKeyframeGroup group, IReadOnlyList<double> cellWidths,
        bool backward = false)
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
        bool wraps = PlaybackOrder.GlidesAcrossLoop(chain.Loop, chain.PingPong);
        int? wrapTo = backward && wraps && match[0] is not null && match[count - 1] is not null
            ? WrapTarget(match, count, group)
            : null;
        if (wrapTo == 0) wrapTo = null; // one keyframe spanning the whole chain: nothing to glide to

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

            if (!backward)
            {
                for (int k = 0; k < keyframes.Count; k++)
                {
                    int from = keyframes[k];
                    keyframeXs.Add(edges[from]);
                    bool isLast = k == keyframes.Count - 1;
                    if (!isLast)
                    {
                        spans.Add(new KeyframeLaneSpan(edges[from], edges[keyframes[k + 1]], SpanCurve(group, match[from]!, match[keyframes[k + 1]]!)));
                        valuesDiffer |= ValuesDiffer(group, match[from]!, match[keyframes[k + 1]]!);
                    }
                    else if (runEnd == count - 1 && wraps && match[0] is not null && from != 0)
                    {
                        spans.Add(new KeyframeLaneSpan(edges[from], edges[count], SpanCurve(group, match[from]!, match[0]!)));
                    }
                    else
                    {
                        holds.Add(new KeyframeLaneHold(edges[from], edges[runEnd + 1]));
                    }
                }
            }
            else
            {
                // Backward (#1538): the span between keyframes J < K covers cells J+1..K, K is reached at its right edge.
                for (int k = 0; k < keyframes.Count; k++)
                {
                    int to = keyframes[k];
                    keyframeXs.Add(edges[to + 1]);
                    if (k > 0)
                    {
                        int from = keyframes[k - 1];
                        spans.Add(new KeyframeLaneSpan(edges[from + 1], edges[to + 1], SpanCurve(group, match[from]!, match[to]!), Backward: true));
                        valuesDiffer |= ValuesDiffer(group, match[from]!, match[to]!);
                    }
                }
                // A run's first cell holds going backward, unless it is frame 0 and glides across the loop point to the
                // last keyframe of the run that ends the chain; that glide also covers the cells after that keyframe.
                if (runStart == 0 && wrapTo is int target)
                    AddBackwardWrap(spans, edges, count, target, SpanCurve(group, match[target]!, match[0]!));
                else
                    holds.Add(new KeyframeLaneHold(edges[runStart], edges[runStart + 1]));
                int last = keyframes[^1];
                bool tailWraps = runEnd == count - 1 && wrapTo is not null;
                if (!tailWraps && last < runEnd) holds.Add(new KeyframeLaneHold(edges[last + 1], edges[runEnd + 1]));
            }

            for (int i = runStart; i <= runEnd; i++) ticks.Add(edges[i + 1]);
            runStart = runEnd;
        }

        bool inUse = valuesDiffer || spans.Any(sp => !sp.Curve.IsSnap);
        return inUse ? new KeyframeLane(keyframeXs, spans, holds, edges[count], ticks) : null;
    }

    // The last keyframe of the run that ends the chain: where a backward glide from frame 0 across the loop arrives.
    private static int WrapTarget(IKeyframeShape?[] match, int count, ShapeKeyframeGroup group)
    {
        int runStart = count - 1;
        while (runStart > 0 && match[runStart - 1] is not null) runStart--;
        for (int i = count - 1; i > runStart; i--)
            if (!IsSkipped(group, match[i]!)) return i;
        return runStart;
    }

    private static bool IsSkipped(ShapeKeyframeGroup group, IKeyframeShape shape) => group switch
    {
        ShapeKeyframeGroup.Offset => shape.SkipOffsetKeyframe,
        ShapeKeyframeGroup.Size => shape.SkipSizeKeyframe,
        _ => shape.SkipPointsKeyframe,
    };

    // The curve playback actually uses over a span: a polygon whose point count changes between two points keyframes snaps.
    private static KeyframeCurve SpanCurve(ShapeKeyframeGroup group, IKeyframeShape from, IKeyframeShape to) =>
        group == ShapeKeyframeGroup.Points && from.Points.Count != to.Points.Count ? KeyframeCurve.Snap : CurveOf(group, from);

    private static KeyframeCurve CurveOf(ShapeKeyframeGroup group, IKeyframeShape shape) => group switch
    {
        ShapeKeyframeGroup.Offset => shape.OffsetCurve,
        ShapeKeyframeGroup.Size => shape.SizeCurve,
        _ => shape.PointsCurve,
    };

    private static bool ValuesDiffer(ShapeKeyframeGroup group, IKeyframeShape a, IKeyframeShape b) => group switch
    {
        ShapeKeyframeGroup.Offset => a.X != b.X || a.Y != b.Y,
        ShapeKeyframeGroup.Size => a.SizeX != b.SizeX || a.SizeY != b.SizeY,
        _ => PointsDiffer(a.Points, b.Points),
    };

    // A changed count counts as a difference too: it snaps at the next keyframe.
    private static bool PointsDiffer(IReadOnlyList<System.Numerics.Vector2> a, IReadOnlyList<System.Numerics.Vector2> b)
    {
        if (a.Count != b.Count) return true;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return true;
        return false;
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
    /// in between). Snap is flat, then jumps at the end, rather than sampled; played <paramref name="backward"/> it
    /// holds the right keyframe and jumps at the left edge instead. A glide's shape is the same either way, since
    /// backward playback retraces it. <paramref name="tStart"/>..<paramref name="tEnd"/> is the part of the curve the
    /// span draws (a piece of a split span). Uses the shared <see cref="KeyframeCurve.Evaluate"/>, so it is exactly
    /// what playback does.
    /// </summary>
    public static IReadOnlyList<(double T, double Progress)> CurvePoints(KeyframeCurve curve, int steps,
        bool backward = false, double tStart = 0, double tEnd = 1)
    {
        if (curve.IsSnap)
        {
            if (!backward) return new (double, double)[] { (0, 0), (1, 0), (1, 1) };
            return tStart <= 0 ? new (double, double)[] { (0, 0), (0, 1), (1, 1) } : new (double, double)[] { (0, 1), (1, 1) };
        }

        steps = Math.Max(1, steps);
        var points = new (double, double)[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            points[i] = (t, curve.Evaluate((float)(tStart + t * (tEnd - tStart))));
        }
        return points;
    }
}
