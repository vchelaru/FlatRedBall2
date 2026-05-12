using System;
using System.Collections.Generic;

namespace FlatRedBall2.Animation;

/// <summary>
/// A named sequence of <see cref="AnimationFrame"/>s. Assign to
/// <see cref="FlatRedBall2.Rendering.Sprite.AnimationChains"/> and play via
/// <see cref="FlatRedBall2.Rendering.Sprite.PlayAnimation(string)"/>.
/// </summary>
public class AnimationChain : List<AnimationFrame>
{
    /// <summary>
    /// Identifier used by <see cref="FlatRedBall2.Rendering.Sprite.PlayAnimation(string)"/> and by
    /// the <see cref="AnimationChainList"/> string indexer to look this chain up.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Total duration of the animation (sum of all frame lengths).</summary>
    public TimeSpan TotalLength
    {
        get
        {
            TimeSpan sum = TimeSpan.Zero;
            foreach (var frame in this)
                sum += frame.FrameLength;
            return sum;
        }
    }
}
