using System;
using System.Collections.Generic;
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
/// </summary>
public static class KeyframeEvaluator
{
    /// <summary>
    /// The offset to display <paramref name="elapsedInFrame"/> into frame <paramref name="frameIndex"/>
    /// of an editor-side chain. Frames with no length use 0.1 (the editor's playback default).
    /// </summary>
    public static (float X, float Y) EvaluateOffset(AnimationChainSave chain, int frameIndex, double elapsedInFrame)
        => EvaluateOffset(SaveKeyframeFrame.Create(chain), chain.Loop, frameIndex, elapsedInFrame);

    /// <summary>The color channels to display, same contract as <see cref="EvaluateOffset(AnimationChainSave, int, double)"/>.</summary>
    public static KeyframeColor EvaluateColor(AnimationChainSave chain, int frameIndex, double elapsedInFrame)
        => EvaluateColor(SaveKeyframeFrame.Create(chain), chain.Loop, frameIndex, elapsedInFrame);

    /// <summary>
    /// The offset to display <paramref name="elapsedInFrame"/> into frame <paramref name="frameIndex"/>.
    /// An empty list yields (0, 0); an out-of-range index clamps.
    /// </summary>
    public static (float X, float Y) EvaluateOffset<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame)
        where TFrame : IKeyframeFrame
    {
        if (frames.Count == 0) return (0f, 0f);
        frameIndex = Math.Clamp(frameIndex, 0, frames.Count - 1);

        FindSegment(frames, loop, frameIndex, elapsedInFrame, static f => f.SkipOffsetKeyframe,
            out int start, out int end, out float t);

        // Snap holds even at t == 1: the next keyframe takes over only when playback moves onto its frame.
        var from = frames[start];
        if (end < 0 || from.OffsetCurve.IsSnap) return (from.RelativeX, from.RelativeY);

        float eased = from.OffsetCurve.Evaluate(t);
        var to = frames[end];
        return (Lerp(from.RelativeX, to.RelativeX, eased), Lerp(from.RelativeY, to.RelativeY, eased));
    }

    /// <summary>
    /// The color channels to display, same contract as
    /// <see cref="EvaluateOffset{TFrame}(IReadOnlyList{TFrame}, bool, int, double)"/>. Each channel
    /// glides independently; a channel unset at one end counts as the operation's identity (255, or 0
    /// for Add) and a channel unset at both stays <c>null</c>. Results round away from zero.
    /// </summary>
    public static KeyframeColor EvaluateColor<TFrame>(
        IReadOnlyList<TFrame> frames, bool loop, int frameIndex, double elapsedInFrame)
        where TFrame : IKeyframeFrame
    {
        if (frames.Count == 0) return default;
        frameIndex = Math.Clamp(frameIndex, 0, frames.Count - 1);

        FindSegment(frames, loop, frameIndex, elapsedInFrame, static f => f.SkipColorKeyframe,
            out int start, out int end, out float t);

        var from = frames[start];
        if (end < 0 || from.ColorCurve.IsSnap)
            return new KeyframeColor(from.Red, from.Green, from.Blue, from.Alpha);

        float eased = from.ColorCurve.Evaluate(t);
        var to = frames[end];
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
        int segmentEnd = end == 0 ? frames.Count : end;
        double duration = 0, position = elapsedInFrame;
        for (int i = start; i < segmentEnd; i++)
        {
            double length = frames[i].DurationSeconds;
            duration += length;
            if (i < frameIndex) position += length;
        }

        t = duration > 0 ? (float)Math.Clamp(position / duration, 0.0, 1.0) : 0f;
    }

    private static float Lerp(float from, float to, float eased)
        => eased <= 0f ? from : eased >= 1f ? to : from + (to - from) * eased;

    private static int? LerpChannel(int? from, int? to, int fromIdentity, int toIdentity, float eased)
    {
        if (from is null && to is null) return null;
        if (eased <= 0f) return from;
        int a = from ?? fromIdentity, b = to ?? toIdentity;
        if (eased >= 1f) return b;
        return (int)Math.Round(a + (b - a) * (double)eased, MidpointRounding.AwayFromZero);
    }
}
