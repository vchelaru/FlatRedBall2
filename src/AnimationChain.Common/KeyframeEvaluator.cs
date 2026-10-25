using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Animation;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// One frame as the keyframe evaluator sees it. Color channels are already resolved through
/// <see cref="EffectiveFrameColor"/> (inherited nulls filled in), so a keyframe's channels can be
/// read directly. Implemented by <see cref="AnimationFrameBase"/>, the engine's frame type, and the
/// editor's save-frame adapter.
/// </summary>
public interface IKeyframeFrame
{
    /// <summary>How long the frame is displayed, in the same unit as the <c>elapsedInFrame</c> passed to the evaluator.</summary>
    double DurationSeconds { get; }

    /// <summary>The frame's own X offset. Ignored when <see cref="SkipOffsetKeyframe"/> is set.</summary>
    float RelativeX { get; }

    /// <summary>The frame's own Y offset. Ignored when <see cref="SkipOffsetKeyframe"/> is set.</summary>
    float RelativeY { get; }

    /// <summary>Resolved red channel, or <c>null</c> if no frame so far set it.</summary>
    int? Red { get; }

    /// <summary>Resolved green channel, or <c>null</c> if no frame so far set it.</summary>
    int? Green { get; }

    /// <summary>Resolved blue channel, or <c>null</c> if no frame so far set it.</summary>
    int? Blue { get; }

    /// <summary>Resolved alpha channel, or <c>null</c> if no frame so far set it.</summary>
    int? Alpha { get; }

    /// <summary>Resolved color operation; only used to pick the identity value for an unset channel.</summary>
    ColorOperation? ColorOperation { get; }

    /// <summary>When <c>true</c> the frame is not an offset keyframe: its own offset and curve are ignored.</summary>
    bool SkipOffsetKeyframe { get; }

    /// <summary>When <c>true</c> the frame is not a color keyframe: its own channels and curve are ignored.</summary>
    bool SkipColorKeyframe { get; }

    /// <summary>The curve from this frame's offset to the next offset keyframe's.</summary>
    KeyframeCurve OffsetCurve { get; }

    /// <summary>The curve from this frame's color to the next color keyframe's.</summary>
    KeyframeCurve ColorCurve { get; }

    /// <summary>The frame's shapes, for shape keyframe interpolation. Empty when it has none.</summary>
    IReadOnlyList<IKeyframeShape> KeyframeShapes { get; }
}

/// <summary>The color channels at one instant of playback; <c>null</c> means no frame ever set the channel.</summary>
public readonly record struct KeyframeColor(int? Red, int? Green, int? Blue, int? Alpha);

/// <summary>
/// Evaluates per-frame keyframe interpolation (#1478). Every frame is a keyframe unless it opts out
/// per group; a keyframe's curve runs to the next keyframe of the same group, spanning the summed
/// frame lengths in between. Groups (Offset: <c>RelativeX/Y</c>; Color: red/green/blue/alpha) are
/// independent. The first frame is always a keyframe; a non-looping chain's last keyframe holds; a
/// looping chain's last keyframe glides back to the first. With the default (snap) curve the result is
/// exactly the frame's own value, so files without interpolation evaluate as they always did.
/// <para>
/// <b>Backward playback</b> (#1538: Reverse, and PingPong's backward half; pass <c>backward: true</c> for a
/// <see cref="PlaybackVisit.IsBackward"/> visit). Showing frame N glides from N's value toward the previous
/// keyframe's, arriving when that frame starts showing, so entering a keyframe always shows its own value in either
/// direction. The segment between keyframes J &lt; K is governed by <b>J's curve</b> (the one that drives J to K going
/// forward), played time-reversed: the value at backward progress p equals the forward value at progress 1 - p, so
/// the backward path retraces the forward one exactly (an Ease Out going forward is slow leaving K and fast arriving
/// at J going backward). K's own curve is not used, because it describes K's way to the <em>next</em> keyframe, not
/// to J. Snap holds the displayed keyframe K until J's frame starts showing. A looping, backward-playing chain glides
/// from frame 0 across the loop point to the last keyframe, under the last keyframe's curve; a non-looping one (and
/// any ping-pong, which turns around instead of wrapping) holds frame 0.
/// </para>
/// </summary>
public static class KeyframeEvaluator
{
    /// <summary>
    /// The offset to display <paramref name="elapsedInFrame"/> into frame <paramref name="frameIndex"/>
    /// of an editor-side chain. Frames with no length use 0.1 (the editor's playback default). Glides wrap
    /// across the loop point only for a looping chain that is not <see cref="AnimationChainSave.PingPong"/>.
    /// Pass <paramref name="backward"/> when the frame is playing backward (see the class remarks).
    /// </summary>
    public static (float X, float Y) EvaluateOffset(AnimationChainSave chain, int frameIndex, double elapsedInFrame, bool backward = false)
        => EvaluateOffset(SaveKeyframeFrame.Create(chain), GlidesAcrossLoop(chain), frameIndex, elapsedInFrame, backward);

    /// <summary>The color channels to display, same contract as <see cref="EvaluateOffset(AnimationChainSave, int, double, bool)"/>.</summary>
    public static KeyframeColor EvaluateColor(AnimationChainSave chain, int frameIndex, double elapsedInFrame, bool backward = false)
        => EvaluateColor(SaveKeyframeFrame.Create(chain), GlidesAcrossLoop(chain), frameIndex, elapsedInFrame, backward);

    private static bool GlidesAcrossLoop(AnimationChainSave chain) => PlaybackOrder.GlidesAcrossLoop(chain.Loop, chain.PingPong);

    /// <summary>
    /// The offset to display <paramref name="elapsedInFrame"/> into frame <paramref name="frameIndex"/>.
    /// An empty list yields (0, 0); an out-of-range index clamps. Back curves (Anticipate, Overshoot)
    /// swing past the start/end values and the result is not clamped. <paramref name="loop"/> means glides wrap
    /// across the loop point (<see cref="PlaybackOrder.GlidesAcrossLoop"/>); <paramref name="backward"/> evaluates
    /// the frame as playing backward (see the class remarks).
    /// </summary>
    public static (float X, float Y) EvaluateOffset<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame, bool backward = false)
        where TFrame : IKeyframeFrame
    {
        if (frames.Count == 0) return (0f, 0f);
        frameIndex = Math.Clamp(frameIndex, 0, frames.Count - 1);

        Resolve(frames, loop, frameIndex, elapsedInFrame, backward, static f => f.SkipOffsetKeyframe,
            static f => f.OffsetCurve, out int a, out int b, out float eased);

        var from = frames[a];
        if (b < 0) return (from.RelativeX, from.RelativeY);
        var to = frames[b];
        return (Lerp(from.RelativeX, to.RelativeX, eased), Lerp(from.RelativeY, to.RelativeY, eased));
    }

    /// <summary>
    /// The color channels to display, same contract as
    /// <see cref="EvaluateOffset{TFrame}(IReadOnlyList{TFrame}, bool, int, double, bool)"/>. Each channel
    /// glides independently; a channel unset at one end counts as the operation's identity (255, or 0
    /// for Add) and a channel unset at both stays <c>null</c>. Results round away from zero. Back
    /// curves (Anticipate, Overshoot) swing past the end values, and the result is returned unclamped
    /// (channels can go below 0 or above 255); the renderer clamps at draw time.
    /// </summary>
    public static KeyframeColor EvaluateColor<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame, bool backward = false)
        where TFrame : IKeyframeFrame
    {
        if (frames.Count == 0) return default;
        frameIndex = Math.Clamp(frameIndex, 0, frames.Count - 1);

        Resolve(frames, loop, frameIndex, elapsedInFrame, backward, static f => f.SkipColorKeyframe,
            static f => f.ColorCurve, out int a, out int b, out float eased);

        var from = frames[a];
        if (b < 0)
            return new KeyframeColor(from.Red, from.Green, from.Blue, from.Alpha);

        var to = frames[b];
        int fromIdentity = EffectiveFrameColor.ChannelDefault(from.ColorOperation);
        int toIdentity = EffectiveFrameColor.ChannelDefault(to.ColorOperation);
        return new KeyframeColor(
            LerpChannel(from.Red, to.Red, fromIdentity, toIdentity, eased),
            LerpChannel(from.Green, to.Green, fromIdentity, toIdentity, eased),
            LerpChannel(from.Blue, to.Blue, fromIdentity, toIdentity, eased),
            LerpChannel(from.Alpha, to.Alpha, 255, 255, eased));
    }

    // Locates the keyframe segment containing frameIndex: `start` is the controlling keyframe, `end` the
    // keyframe it glides to (-1 = nothing to glide to, hold `start`), `t` the linear progress through it.
    private static void FindSegment<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame,
        Func<TFrame, bool> isSkipped, out int start, out int end, out float t)
        where TFrame : IKeyframeFrame
    {
        start = frameIndex;
        while (start > 0 && isSkipped(frames[start])) start--;

        end = frameIndex + 1;
        while (end < frames.Count && isSkipped(frames[end])) end++;
        if (end >= frames.Count)
            end = loop ? 0 : -1;

        t = 0f;
        // Nothing to glide to: a non-looping chain's last keyframe, or a looping chain with one keyframe.
        if (end < 0 || end == start) { end = -1; return; }
        t = Progress(frames, start, end, frameIndex, elapsedInFrame);
    }

    // The value is Lerp(frames[a], frames[b], eased); b < 0 means hold frames[a]. Applies the curve and its
    // direction, so callers only lerp.
    private static void Resolve<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame, bool backward,
        Func<TFrame, bool> isSkipped, Func<TFrame, KeyframeCurve> curveOf, out int a, out int b, out float eased)
        where TFrame : IKeyframeFrame
    {
        eased = 0f;
        if (!backward)
        {
            FindSegment(frames, loop, frameIndex, elapsedInFrame, isSkipped, out a, out b, out float t);
            // Snap holds even at t == 1: the next keyframe takes over only when playback moves onto its frame.
            if (b < 0 || curveOf(frames[a]).IsSnap) { b = -1; return; }
            eased = curveOf(frames[a]).Evaluate(t);
            return;
        }

        FindSegmentBackward(frames, loop, frameIndex, elapsedInFrame, isSkipped, out int shown, out int previous, out float p);
        // The earlier keyframe's curve owns the segment, retraced: backward progress p is forward progress 1 - p.
        if (previous < 0 || curveOf(frames[previous]).IsSnap) { a = shown; b = -1; return; }
        a = previous;
        b = shown;
        eased = curveOf(frames[previous]).Evaluate(1f - p);
    }

    // Backward counterpart of FindSegment: `shown` is the keyframe whose value playback left most recently (the
    // nearest keyframe at or after frameIndex, or frame 0 across the loop point), `previous` the keyframe it glides
    // back to (-1 = hold `shown`), `p` the linear progress from `shown` toward `previous`.
    private static void FindSegmentBackward<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame,
        Func<TFrame, bool> isSkipped, out int shown, out int previous, out float p)
        where TFrame : IKeyframeFrame
    {
        int n = frames.Count;
        p = 0f;
        previous = -1;

        shown = frameIndex;
        while (shown < n && shown > 0 && isSkipped(frames[shown])) shown++;
        if (shown >= n)
        {
            if (!loop)
            {
                // Past the last keyframe of a non-looping chain: it holds, as it does going forward.
                shown = frameIndex;
                while (shown > 0 && isSkipped(frames[shown])) shown--;
                return;
            }
            shown = 0;
        }

        if (shown > 0)
        {
            previous = shown - 1;
            while (previous > 0 && isSkipped(frames[previous])) previous--;
        }
        else
        {
            if (!loop) return;
            previous = n - 1;
            while (previous > 0 && isSkipped(frames[previous])) previous--;
            // A looping chain with one keyframe has nothing to glide to.
            if (previous == 0) { previous = -1; return; }
        }
        p = BackwardProgress(frames, shown, previous, frameIndex, elapsedInFrame);
    }

    // Linear progress through a backward segment, which shows frames shown, shown-1, ..., previous+1 (cyclically,
    // so frame 0 then the last frames for the loop-point segment), at `elapsedInFrame` into `frameIndex`.
    private static float BackwardProgress<TFrame>(IReadOnlyList<TFrame> frames, int shown, int previous, int frameIndex, double elapsedInFrame)
        where TFrame : IKeyframeFrame
    {
        int n = frames.Count;
        double duration = 0, position = elapsedInFrame;
        bool reached = false;
        for (int i = shown; i != previous; i = (i - 1 + n) % n)
        {
            double length = frames[i].DurationSeconds;
            duration += length;
            if (i == frameIndex) reached = true;
            else if (!reached) position += length;
        }
        return duration > 0 ? (float)Math.Clamp(position / duration, 0.0, 1.0) : 0f;
    }

    // ---- Shapes (#1478) -------------------------------------------------------------------------

    /// <summary>
    /// The offset and size of <paramref name="shape"/> (a shape of frame <paramref name="frameIndex"/>)
    /// <paramref name="elapsedInFrame"/> into that frame. Shapes match across frames by name and kind: a
    /// glide runs to the next keyframe of the same group among the following frames that hold the name,
    /// and stops where the name is missing, duplicated, or changes kind. A shape with no name, or whose
    /// name is duplicated in its own frame, snaps. Offset and size are independent groups, each with its
    /// own skip flag and curve; the first frame of a run of frames holding the name is always a keyframe.
    /// A shape that is not in the frame returns its own values. Back curves swing past the endpoints.
    /// </summary>
    public static KeyframeShapeValue EvaluateShape(AnimationChainSave chain, int frameIndex, double elapsedInFrame, ShapeSave shape,
        bool backward = false)
    {
        var frames = SaveKeyframeFrame.Create(chain);
        frameIndex = Math.Clamp(frameIndex, 0, Math.Max(frames.Length - 1, 0));
        var adapter = SaveKeyframeShape.Create(shape);
        if (frames.Length > 0)
            foreach (var candidate in frames[frameIndex].KeyframeShapes)
                if (candidate is SaveKeyframeShape saved && ReferenceEquals(saved.Source, shape))
                {
                    adapter = saved;
                    break;
                }
        return EvaluateShape(frames, GlidesAcrossLoop(chain), frameIndex, elapsedInFrame, adapter, backward);
    }

    /// <summary>
    /// Same contract as <see cref="EvaluateShape(AnimationChainSave, int, double, ShapeSave, bool)"/> over any frame
    /// list. <paramref name="shape"/> is looked up in frame <paramref name="frameIndex"/> by name and kind.
    /// </summary>
    public static KeyframeShapeValue EvaluateShape<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame, IKeyframeShape shape,
        bool backward = false)
        where TFrame : IKeyframeFrame
    {
        var own = new KeyframeShapeValue(shape.X, shape.Y, shape.SizeX, shape.SizeY);
        if (frames.Count == 0) return own;
        frameIndex = Math.Clamp(frameIndex, 0, frames.Count - 1);
        if (!TryMatch(frames[frameIndex], shape.Name, shape.Kind, out var current)) return own;

        ResolveShape(frames, loop, frameIndex, elapsedInFrame, current, backward, static s => s.SkipOffsetKeyframe,
            static s => s.OffsetCurve, out var offsetHold, out var offsetFrom, out var offsetTo, out float offsetEased);
        ResolveShape(frames, loop, frameIndex, elapsedInFrame, current, backward, static s => s.SkipSizeKeyframe,
            static s => s.SizeCurve, out var sizeHold, out var sizeFrom, out var sizeTo, out float sizeEased);

        float x = offsetHold.X, y = offsetHold.Y;
        if (offsetTo is not null)
        {
            x = Lerp(offsetFrom.X, offsetTo.X, offsetEased);
            y = Lerp(offsetFrom.Y, offsetTo.Y, offsetEased);
        }

        float sizeX = sizeHold.SizeX, sizeY = sizeHold.SizeY;
        if (sizeTo is not null)
        {
            sizeX = Lerp(sizeFrom.SizeX, sizeTo.SizeX, sizeEased);
            sizeY = Lerp(sizeFrom.SizeY, sizeTo.SizeY, sizeEased);
        }

        return new KeyframeShapeValue(x, y, sizeX, sizeY);
    }

    /// <summary>
    /// The points of <paramref name="polygon"/> (a polygon of frame <paramref name="frameIndex"/>)
    /// <paramref name="elapsedInFrame"/> into that frame (#1484). Points are their own keyframe group, matched across
    /// frames like <see cref="EvaluateShape(AnimationChainSave, int, double, ShapeSave, bool)"/> (by name and kind, the first
    /// frame of a run is always a keyframe). Point i glides to point i of the next points keyframe; when the counts
    /// differ the glide snaps and the earlier keyframe's points hold until the next frame plays. Back curves swing past
    /// the endpoints. A polygon that is not in the frame returns its own points.
    /// </summary>
    public static IReadOnlyList<Vector2> EvaluateShapePoints(AnimationChainSave chain, int frameIndex, double elapsedInFrame, PolygonSave polygon,
        bool backward = false)
    {
        var frames = SaveKeyframeFrame.Create(chain);
        frameIndex = Math.Clamp(frameIndex, 0, Math.Max(frames.Length - 1, 0));
        var adapter = SaveKeyframeShape.Create(polygon);
        if (frames.Length > 0)
            foreach (var candidate in frames[frameIndex].KeyframeShapes)
                if (candidate is SaveKeyframeShape saved && ReferenceEquals(saved.Source, polygon))
                {
                    adapter = saved;
                    break;
                }
        return EvaluateShapePoints(frames, GlidesAcrossLoop(chain), frameIndex, elapsedInFrame, adapter, backward);
    }

    /// <summary>
    /// Same contract as <see cref="EvaluateShapePoints(AnimationChainSave, int, double, PolygonSave, bool)"/> over any frame
    /// list. Returns the keyframe's own list (no copy) whenever nothing glides.
    /// </summary>
    public static IReadOnlyList<Vector2> EvaluateShapePoints<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame, IKeyframeShape shape,
        bool backward = false)
        where TFrame : IKeyframeFrame
    {
        var own = shape.Points;
        if (frames.Count == 0) return own;
        frameIndex = Math.Clamp(frameIndex, 0, frames.Count - 1);
        if (!TryMatch(frames[frameIndex], shape.Name, shape.Kind, out var current)) return own;

        ResolveShape(frames, loop, frameIndex, elapsedInFrame, current, backward, static s => s.SkipPointsKeyframe,
            static s => s.PointsCurve, out var hold, out var from, out var to, out float eased);

        if (to is null) return hold.Points;
        var fromPoints = from.Points;
        var toPoints = to.Points;
        // Differing counts snap: the keyframe playback left most recently holds.
        if (fromPoints.Count != toPoints.Count) return hold.Points;

        var result = new Vector2[fromPoints.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = new Vector2(Lerp(fromPoints[i].X, toPoints[i].X, eased), Lerp(fromPoints[i].Y, toPoints[i].Y, eased));
        return result;
    }

    /// <summary>
    /// Whether <paramref name="shape"/> of frame <paramref name="frameIndex"/> is the first frame of a run
    /// of frames that hold its name (the previous frame lacks it, or it is frame 0). Such a shape is always a
    /// keyframe, whatever its skip flags say.
    /// </summary>
    public static bool IsShapeRunStart(AnimationChainSave chain, int frameIndex, ShapeSave shape)
    {
        if (frameIndex <= 0 || frameIndex >= chain.Frames.Count) return true;
        return !TryMatch(SaveKeyframeFrame.Create(chain)[frameIndex - 1], shape.Name, SaveKeyframeShape.KindOf(shape), out _);
    }

    /// <summary>
    /// The one shape in <paramref name="frame"/> with this name, when exactly one has it and it is of the given
    /// kind. False for an empty name, a name held twice (ambiguous), or one of another kind: such a shape snaps.
    /// </summary>
    public static bool TryMatch(IKeyframeFrame frame, string name, KeyframeShapeKind kind, out IKeyframeShape match)
    {
        match = null!;
        if (string.IsNullOrEmpty(name)) return false;
        var shapes = frame.KeyframeShapes;
        IKeyframeShape? found = null;
        for (int i = 0; i < shapes.Count; i++)
        {
            if (shapes[i].Name != name) continue;
            if (found is not null) return false; // duplicate name in one frame: ambiguous
            found = shapes[i];
        }
        if (found is null || found.Kind != kind) return false;
        match = found;
        return true;
    }

    // Shape counterpart of FindSegment: `from` is the controlling keyframe of `current`'s name, `to` the
    // keyframe it glides to (null = hold `from`), `t` the linear progress. The segment only crosses frames
    // that hold the name; the walk back and forward both stop at the first frame that does not.
    private static void FindShapeSegment<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame,
        IKeyframeShape current, Func<IKeyframeShape, bool> isSkipped,
        out IKeyframeShape from, out IKeyframeShape? to, out float t)
        where TFrame : IKeyframeFrame
    {
        string name = current.Name;
        var kind = current.Kind;

        int start = frameIndex;
        from = current;
        while (start > 0 && isSkipped(from) && TryMatch(frames[start - 1], name, kind, out var previous))
        {
            start--;
            from = previous;
        }

        int end = -1;
        to = null;
        for (int i = start + 1; ; i++)
        {
            bool wrapped = false;
            if (i >= frames.Count)
            {
                if (!loop) break;
                i = 0;
                wrapped = true;
            }
            if (!TryMatch(frames[i], name, kind, out var next)) break;
            // Frame 0 is the first frame of a run, so it is a keyframe whatever its flag says.
            if (wrapped || !isSkipped(next))
            {
                end = i;
                to = next;
                break;
            }
        }

        t = 0f;
        // Nothing to glide to: the name stops, a non-looping chain ends, or a looping run holds one keyframe.
        if (end < 0 || end == start) { to = null; return; }
        t = Progress(frames, start, end, frameIndex, elapsedInFrame);
    }

    // Shape counterpart of Resolve. Gliding: Lerp(from, to, eased). Not gliding (`to` null): show `hold`, the
    // keyframe playback left most recently (forward: the controlling keyframe; backward: the displayed one).
    private static void ResolveShape<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame, IKeyframeShape current,
        bool backward, Func<IKeyframeShape, bool> isSkipped, Func<IKeyframeShape, KeyframeCurve> curveOf,
        out IKeyframeShape hold, out IKeyframeShape from, out IKeyframeShape? to, out float eased)
        where TFrame : IKeyframeFrame
    {
        eased = 0f;
        if (!backward)
        {
            FindShapeSegment(frames, loop, frameIndex, elapsedInFrame, current, isSkipped, out from, out to, out float t);
            hold = from;
            if (to is null || curveOf(from).IsSnap) { to = null; return; }
            eased = curveOf(from).Evaluate(t);
            return;
        }

        FindShapeSegmentBackward(frames, loop, frameIndex, elapsedInFrame, current, isSkipped,
            out var shown, out var previous, out float p);
        hold = shown;
        from = shown;
        to = null;
        if (previous is null || curveOf(previous).IsSnap) return;
        from = previous;
        to = shown;
        eased = curveOf(previous).Evaluate(1f - p);
    }

    // Backward counterpart of FindShapeSegment, by the same name-run rules: `shown` is the keyframe at or after
    // frameIndex within the run (frame 0 across the loop point), `previous` the keyframe before it (null = hold).
    private static void FindShapeSegmentBackward<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame,
        IKeyframeShape current, Func<IKeyframeShape, bool> isSkipped,
        out IKeyframeShape shown, out IKeyframeShape? previous, out float p)
        where TFrame : IKeyframeFrame
    {
        string name = current.Name;
        var kind = current.Kind;
        int n = frames.Count;
        p = 0f;
        previous = null;

        // The first frame of a run of frames holding the name is always a keyframe.
        bool IsKeyframe(int i, IKeyframeShape s) => !isSkipped(s) || i == 0 || !TryMatch(frames[i - 1], name, kind, out _);

        int shownIndex = frameIndex;
        shown = current;
        while (!IsKeyframe(shownIndex, shown))
        {
            int next = shownIndex + 1;
            if (next >= n)
            {
                if (!loop) { shown = HoldEarlier(frames, frameIndex, current, isSkipped); return; }
                next = 0;
            }
            if (!TryMatch(frames[next], name, kind, out var nextShape)) { shown = HoldEarlier(frames, frameIndex, current, isSkipped); return; }
            shownIndex = next;
            shown = nextShape;
        }

        int previousIndex = shownIndex - 1;
        if (previousIndex < 0)
        {
            if (!loop) return;
            previousIndex = n - 1;
        }
        while (previousIndex != shownIndex && previousIndex >= 0)
        {
            if (!TryMatch(frames[previousIndex], name, kind, out var candidate)) return;
            if (IsKeyframe(previousIndex, candidate))
            {
                previous = candidate;
                p = BackwardProgress(frames, shownIndex, previousIndex, frameIndex, elapsedInFrame);
                return;
            }
            previousIndex--;
        }
    }

    // Past a run's last keyframe with no keyframe after it: that keyframe holds, as it does going forward.
    private static IKeyframeShape HoldEarlier<TFrame>(IReadOnlyList<TFrame> frames, int frameIndex, IKeyframeShape current,
        Func<IKeyframeShape, bool> isSkipped)
        where TFrame : IKeyframeFrame
    {
        int start = frameIndex;
        var held = current;
        while (start > 0 && isSkipped(held) && TryMatch(frames[start - 1], current.Name, current.Kind, out var earlier))
        {
            start--;
            held = earlier;
        }
        return held;
    }

    // Linear progress through the segment from `start` to `end` (end == 0 means the loop wrap, which
    // spans through the last frame), at `elapsedInFrame` into `frameIndex`.
    private static float Progress<TFrame>(IReadOnlyList<TFrame> frames, int start, int end, int frameIndex, double elapsedInFrame)
        where TFrame : IKeyframeFrame
    {
        int segmentEnd = end == 0 ? frames.Count : end;
        double duration = 0, position = elapsedInFrame;
        for (int i = start; i < segmentEnd; i++)
        {
            double length = frames[i].DurationSeconds;
            duration += length;
            if (i < frameIndex) position += length;
        }
        return duration > 0 ? (float)Math.Clamp(position / duration, 0.0, 1.0) : 0f;
    }

    // Endpoints are exact (KeyframeCurve.Evaluate returns exactly 0 and 1 there); anything else, including
    // Back-curve swings below 0 or above 1, is the raw interpolation. Clamping is the renderers' job.
    private static float Lerp(float from, float to, float eased)
        => eased == 0f ? from : eased == 1f ? to : from + (to - from) * eased;

    private static int? LerpChannel(int? from, int? to, int fromIdentity, int toIdentity, float eased)
    {
        if (from is null && to is null) return null;
        if (eased == 0f) return from;
        int a = from ?? fromIdentity, b = to ?? toIdentity;
        if (eased == 1f) return b;
        return (int)Math.Round(a + (b - a) * (double)eased, MidpointRounding.AwayFromZero);
    }
}
