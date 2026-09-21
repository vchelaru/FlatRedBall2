using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Propagates one resized frame's new width/height onto every other frame in the same chain, for
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
    /// <paramref name="resizedFrame"/>, each keeping its own Left/Top (its position in the sprite
    /// sheet) while its Right/Bottom move to match <paramref name="newWidth"/>/<paramref
    /// name="newHeight"/> (the resized frame's new UV size). Empty when the chain has no other
    /// frames.
    /// </summary>
    public static IReadOnlyList<SiblingMatch> ComputeSiblingMatches(
        AnimationChainSave chain, AnimationFrameSave resizedFrame, float newWidth, float newHeight)
    {
        var result = new List<SiblingMatch>();
        foreach (var frame in chain.Frames)
        {
            if (ReferenceEquals(frame, resizedFrame)) continue;
            var before = new FrameRect(frame.LeftCoordinate, frame.TopCoordinate, frame.RightCoordinate, frame.BottomCoordinate);
            var after = new FrameRect(frame.LeftCoordinate, frame.TopCoordinate,
                frame.LeftCoordinate + newWidth, frame.TopCoordinate + newHeight);
            result.Add(new SiblingMatch(frame, before, after));
        }
        return result;
    }
}
