using FlatRedBall2.Animation;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A frame's per-channel color after inheritance: each channel is the most recent value set at or
/// before the frame in its chain, or <c>null</c> if no frame so far set it. Channels resolve independently.
/// </summary>
public readonly record struct ResolvedFrameColor(
    int? Red, int? Green, int? Blue, int? Alpha, ColorOperation? Operation);

/// <summary>
/// Resolves a frame's <em>effective</em> color. A <c>null</c> <see cref="AnimationFrameSave.Red"/>/
/// <see cref="AnimationFrameSave.Green"/>/<see cref="AnimationFrameSave.Blue"/>/<see cref="AnimationFrameSave.Alpha"/>/
/// <see cref="AnimationFrameSave.ColorOperation"/> inherits the most recent earlier frame in the same chain
/// that sets it; inheritance never crosses chains. The FlatRedBall2 engine, the
/// FlatRedBall.AnimationChain.MonoGame/KNI packages, and the AnimationEditor all resolve through this
/// type so the editor preview matches the game.
/// </summary>
public static class EffectiveFrameColor
{
    /// <summary>
    /// Resolves the color of the frame at <paramref name="frameIndex"/>. Out-of-range indices clamp;
    /// a negative index yields an all-<c>null</c> result. Prefer <see cref="ResolveAll"/> when
    /// resolving every frame of a chain.
    /// </summary>
    public static ResolvedFrameColor Resolve(IReadOnlyList<AnimationFrameSave> frames, int frameIndex)
    {
        int? red = null, green = null, blue = null, alpha = null;
        ColorOperation? operation = null;

        int start = Math.Min(frameIndex, frames.Count - 1);
        for (int i = start; i >= 0; i--)
        {
            var f = frames[i];
            red       ??= f.Red;
            green     ??= f.Green;
            blue      ??= f.Blue;
            alpha     ??= f.Alpha;
            operation ??= f.ColorOperation;
        }

        return new ResolvedFrameColor(red, green, blue, alpha, operation);
    }

    /// <summary>
    /// Resolves every frame of one chain in a single forward O(n) pass. Result <c>[i]</c> equals
    /// <c>Resolve(frames, i)</c>. Returns an empty array for an empty list.
    /// </summary>
    public static ResolvedFrameColor[] ResolveAll(IReadOnlyList<AnimationFrameSave> frames)
    {
        var result = new ResolvedFrameColor[frames.Count];
        int? red = null, green = null, blue = null, alpha = null;
        ColorOperation? operation = null;

        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            // A set channel overrides the running value; `??=` would freeze on the first set value.
            red       = f.Red           ?? red;
            green     = f.Green         ?? green;
            blue      = f.Blue          ?? blue;
            alpha     = f.Alpha         ?? alpha;
            operation = f.ColorOperation ?? operation;
            result[i] = new ResolvedFrameColor(red, green, blue, alpha, operation);
        }

        return result;
    }

    /// <summary>
    /// The R/G/B identity value for a color operation, i.e. what an unset channel contributes:
    /// 0 for <see cref="ColorOperation.Add"/>, 255 otherwise.
    /// </summary>
    public static int ChannelDefault(ColorOperation? operation)
        => operation == ColorOperation.Add ? 0 : 255;
}
