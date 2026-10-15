using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Keyframes;

/// <summary>The offset and resolved color to display at one instant of a chain's playback.</summary>
public readonly record struct KeyframeSample(float OffsetX, float OffsetY, ResolvedFrameColor Color);

/// <summary>
/// Samples a chain the way the runtime plays it: the curves stored on each frame glide offset and
/// color between keyframes (#1478), through the shared <see cref="KeyframeEvaluator"/>. The preview,
/// the animated-image export and the inspector's read-only values all go through here.
/// </summary>
public static class KeyframeSampler
{
    /// <summary>
    /// The offset and color <paramref name="elapsedInFrame"/> seconds into frame <paramref name="frameIndex"/>
    /// (clamped to the chain). A missing or empty chain yields zero and unset color. The color operation is
    /// the frame's resolved one; it never glides.
    /// </summary>
    public static KeyframeSample Sample(AnimationChainSave? chain, int frameIndex, double elapsedInFrame)
    {
        if (chain is null || chain.Frames.Count == 0) return default;
        frameIndex = Math.Clamp(frameIndex, 0, chain.Frames.Count - 1);

        var frames = SaveKeyframeFrame.Create(chain);
        var (x, y) = KeyframeEvaluator.EvaluateOffset(frames, chain.Loop, frameIndex, elapsedInFrame);
        var c = KeyframeEvaluator.EvaluateColor(frames, chain.Loop, frameIndex, elapsedInFrame);
        return new KeyframeSample(x, y, new ResolvedFrameColor(c.Red, c.Green, c.Blue, c.Alpha, frames[frameIndex].ColorOperation));
    }

    /// <summary>Whether any frame of <paramref name="chain"/> has a curve that glides, so playback must repaint every tick.</summary>
    public static bool IsAnimated(AnimationChainSave? chain)
    {
        if (chain is null) return false;
        foreach (var frame in chain.Frames)
        {
            if (!KeyframeCurve.Parse(frame.OffsetInterpolation, frame.OffsetEasing).IsSnap) return true;
            if (!KeyframeCurve.Parse(frame.ColorInterpolation, frame.ColorEasing).IsSnap) return true;
        }
        return false;
    }
}
