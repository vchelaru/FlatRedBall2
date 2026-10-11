using System;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Computes the position offset (<c>RelativeX/Y</c>) to display during preview playback.
/// <para>
/// In snap mode the offset is exactly the current frame's — this mirrors the FlatRedBall
/// runtime, which overwrites the sprite position on every frame switch and never blends.
/// In interpolate mode the offset eases linearly toward the next frame across the current
/// frame's display time. The last frame eases toward frame 0 when the chain loops and holds
/// its own offset when it does not. Interpolation is a <b>preview-only authoring aid</b>: it is not stored in
/// the <c>.achx</c> and the runtime does not reproduce it.
/// </para>
/// </summary>
public static class OffsetInterpolator
{
    /// <param name="chain">The chain being previewed.</param>
    /// <param name="frameIndex">Index of the currently displayed frame (clamped to the chain).</param>
    /// <param name="frameElapsed">Seconds elapsed within the current frame
    /// (see <see cref="CommandsAndState.PlaybackController.FrameElapsed"/>).</param>
    /// <param name="interpolate">When <c>false</c>, returns the current frame's offset unchanged (snap).</param>
    /// <returns>The (X, Y) offset, in stored units, to render the sprite at.</returns>
    public static (float X, float Y) ComputeOffset(
        AnimationChainSave? chain, int frameIndex, double frameElapsed, bool interpolate)
    {
        if (chain is null || chain.Frames.Count == 0) return (0f, 0f);

        frameIndex = Math.Clamp(frameIndex, 0, chain.Frames.Count - 1);
        var current = chain.Frames[frameIndex];

        // A looping chain's last frame eases back toward frame 0. A non-looping chain's last
        // frame has no successor, so it holds and the clip settles on its final position.
        int lastIndex = chain.Frames.Count - 1;
        if (!interpolate || lastIndex == 0 || (frameIndex == lastIndex && !chain.Loop))
            return (current.RelativeX, current.RelativeY);

        var next = chain.Frames[frameIndex == lastIndex ? 0 : frameIndex + 1];

        // 0.1s default matches PlaybackController's handling of unset frame lengths.
        double frameLength = current.FrameLength > 0 ? current.FrameLength : 0.1;
        float t = (float)Math.Clamp(frameElapsed / frameLength, 0.0, 1.0);

        return (
            current.RelativeX + (next.RelativeX - current.RelativeX) * t,
            current.RelativeY + (next.RelativeY - current.RelativeY) * t);
    }
}
