using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Rendering
{
    /// <summary>
    /// A frame's per-channel color after applying the runtime "unset = keep the last value"
    /// (sticky) semantic. Each channel is the most recent explicitly-set value at or before the
    /// resolved frame, or <c>null</c> if no frame ever set it. Channels resolve independently.
    /// </summary>
    public readonly record struct ResolvedFrameColor(
        int? Red, int? Green, int? Blue, int? Alpha, ColorOperation? Operation);

    /// <summary>
    /// Resolves the <em>effective</em> color of a frame under the sticky semantic runtimes use:
    /// an omitted channel doesn't reset, it holds whatever an earlier frame last set. The editor
    /// climbs backward so its preview and inspector match that behavior instead of resetting every
    /// frame. Where a channel is genuinely never set, the value stays <c>null</c> and each caller
    /// supplies its own baseline (preview: per-operation identity; inspector: a display default).
    /// </summary>
    public static class EffectiveFrameColor
    {
        /// <summary>
        /// Walks backward from <paramref name="frameIndex"/> through <paramref name="frames"/>,
        /// taking each channel's first explicitly-set value. Out-of-range indices clamp; a negative
        /// index yields an all-<c>null</c> result.
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
        /// Resolves the effective color of <em>every</em> frame in one forward O(n) pass, so callers
        /// that tint a whole timeline strip don't pay a per-frame backward <see cref="Resolve"/> walk
        /// (O(n²) overall). Result <c>[i]</c> equals <c>Resolve(frames, i)</c>. Returns an empty array
        /// for an empty list.
        /// </summary>
        public static ResolvedFrameColor[] ResolveAll(IReadOnlyList<AnimationFrameSave> frames)
        {
            var result = new ResolvedFrameColor[frames.Count];
            int? red = null, green = null, blue = null, alpha = null;
            ColorOperation? operation = null;

            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                // Sticky: a frame that explicitly sets a channel overrides the running value; an omitted
                // channel keeps the most recent set value. (`??=` would freeze on the first set — wrong.)
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
        /// The R/G/B identity value for a color operation — what a channel contributes when unset:
        /// black (0) for <see cref="ColorOperation.Add"/> (adds nothing), white (255) otherwise.
        /// Used as the inspector's never-set placeholder so the ghost value matches the preview.
        /// </summary>
        public static int ChannelDefault(ColorOperation? operation)
            => operation == ColorOperation.Add ? 0 : 255;
    }
}
