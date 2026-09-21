using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Propagates one resized frame's per-edge movement onto every other frame in the same chain, for
/// the native <c>.tsx</c> multi-tile-footprint workflow: <see cref="MultiTileToTiledAnimationMapper"/>
/// requires every frame in a chain to share the exact same whole-tile footprint, and silently
/// drops the chain's entire tile animation on save the moment one frame's size disagrees with the
/// rest (see its "frame size ... doesn't match the chain's footprint" check). Without this, a user
/// stretching a single frame to span multiple tiles (the intended way to author a multi-tile
/// animation) leaves every other frame at the old, now-mismatched size.
/// </summary>
public static class FrameFootprintSync
{
    /// <summary>An axis-aligned UV rect, before or after a resize.</summary>
    public readonly record struct FrameRect(float Left, float Top, float Right, float Bottom);

    /// <summary>One sibling frame's before/after rect for a footprint-matching resize.</summary>
    public readonly record struct SiblingMatch(AnimationFrameSave Frame, FrameRect Before, FrameRect After);

    /// <summary>
    /// Returns the before/after rect for every frame in <paramref name="chain"/> other than
    /// <paramref name="resizedFrame"/>, applying the same per-edge delta (<paramref
    /// name="resizedAfter"/> minus <paramref name="resizedBefore"/>, edge by edge) to each
    /// sibling's own rect. This mirrors whichever edge the user actually dragged -- growing
    /// <paramref name="resizedFrame"/> leftward moves every sibling's Left the same way, not just
    /// its Right/Bottom, so a chain resized by pulling a left or top handle grows every frame in
    /// that same direction instead of always rightward/downward. Empty when the chain has no
    /// other frames.
    /// </summary>
    public static IReadOnlyList<SiblingMatch> ComputeSiblingMatches(
        AnimationChainSave chain, AnimationFrameSave resizedFrame, FrameRect resizedBefore, FrameRect resizedAfter) =>
        ComputeSiblingMatches([(resizedFrame, resizedBefore, resizedAfter)], _ => chain);

    /// <summary>
    /// The many-frames form of <see cref="ComputeSiblingMatches(AnimationChainSave, AnimationFrameSave, FrameRect, FrameRect)"/>
    /// for a bulk drag: every chain touched by <paramref name="resized"/> gets its un-dragged
    /// frames matched to that chain's first dragged frame whose size actually changed. A dragged
    /// frame is never also a sibling, and a chain whose dragged frames only moved needs nothing.
    /// </summary>
    public static IReadOnlyList<SiblingMatch> ComputeSiblingMatches(
        IReadOnlyList<(AnimationFrameSave Frame, FrameRect Before, FrameRect After)> resized,
        Func<AnimationFrameSave, AnimationChainSave?> chainOf)
    {
        var resizedFrames = new HashSet<AnimationFrameSave>(resized.Select(r => r.Frame), ReferenceEqualityComparer.Instance);
        var handledChains = new HashSet<AnimationChainSave>(ReferenceEqualityComparer.Instance);
        var result = new List<SiblingMatch>();
        foreach (var (resizedFrame, before, after) in resized)
        {
            if (!SizeChanged(before, after)) continue;
            var chain = chainOf(resizedFrame);
            if (chain is null || !handledChains.Add(chain)) continue;

            float dLeft   = after.Left   - before.Left;
            float dTop    = after.Top    - before.Top;
            float dRight  = after.Right  - before.Right;
            float dBottom = after.Bottom - before.Bottom;
            foreach (var frame in chain.Frames)
            {
                if (resizedFrames.Contains(frame)) continue;
                var siblingBefore = new FrameRect(frame.LeftCoordinate, frame.TopCoordinate, frame.RightCoordinate, frame.BottomCoordinate);
                var siblingAfter = new FrameRect(
                    siblingBefore.Left + dLeft, siblingBefore.Top + dTop,
                    siblingBefore.Right + dRight, siblingBefore.Bottom + dBottom);
                result.Add(new SiblingMatch(frame, siblingBefore, siblingAfter));
            }
        }
        return result;
    }

    /// <summary>Whether a rect change altered the frame's size at all (a pure move keeps every
    /// sibling's footprint valid and so propagates nothing).</summary>
    public static bool SizeChanged(FrameRect before, FrameRect after) =>
        Math.Abs((before.Right - before.Left) - (after.Right - after.Left)) > 0.0001f
        || Math.Abs((before.Bottom - before.Top) - (after.Bottom - after.Top)) > 0.0001f;
}
