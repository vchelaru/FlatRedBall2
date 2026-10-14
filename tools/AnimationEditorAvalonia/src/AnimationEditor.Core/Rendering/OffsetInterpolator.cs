using System;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Computes the position offset (<c>RelativeX/Y</c>) to display during preview playback.
/// <para>
/// In snap mode the offset is exactly the current frame's, regardless of any curve stored in the file.
/// In interpolate mode the offset eases linearly toward the next frame across the current
/// frame's display time, whatever the file stores: the preview's Interpolate toggle is an authoring aid
/// that predates per-frame curves. The last frame eases toward frame 0 when the chain loops and holds
/// its own offset when it does not. The math is the shared <see cref="KeyframeEvaluator"/> that the
/// runtime uses; this class only forces every frame to a linear keyframe.
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
        if (!interpolate || chain.Frames.Count == 1) return (current.RelativeX, current.RelativeY);

        var frames = new LinearOffsetFrame[chain.Frames.Count];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = new LinearOffsetFrame(chain.Frames[i]);
        return KeyframeEvaluator.EvaluateOffset(frames, chain.Loop, frameIndex, frameElapsed);
    }

    // A frame reduced to an always-keyframe, linear-offset frame. Color is unused by the offset path.
    private readonly struct LinearOffsetFrame(AnimationFrameSave frame) : IKeyframeFrame
    {
        // 0.1s default matches PlaybackController's handling of unset frame lengths.
        public double DurationSeconds => frame.FrameLength > 0 ? frame.FrameLength : 0.1;
        public float RelativeX => frame.RelativeX;
        public float RelativeY => frame.RelativeY;
        public int? Red => null;
        public int? Green => null;
        public int? Blue => null;
        public int? Alpha => null;
        public ColorOperation? ColorOperation => null;
        public bool SkipOffsetKeyframe => false;
        public bool SkipColorKeyframe => false;
        public KeyframeCurve OffsetCurve => LinearCurve;
        public KeyframeCurve ColorCurve => default;

        private static readonly KeyframeCurve LinearCurve = KeyframeCurve.Parse("Linear", null);
    }
}
