using System.Collections.Generic;

namespace FlatRedBall2.Animation;

/// <summary>
/// A named sequence of <see cref="AnimationFrame"/>s. Assign to
/// <see cref="FlatRedBall2.Rendering.Sprite.AnimationChains"/> and play via
/// <see cref="FlatRedBall2.Rendering.Sprite.PlayAnimation(string)"/>.
/// </summary>
public class AnimationChain : List<AnimationFrame>
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Total duration of the animation in seconds (sum of all frame lengths).</summary>
    public float TotalLength
    {
        get
        {
            float sum = 0f;
            foreach (var frame in this)
                sum += frame.FrameLength;
            return sum;
        }
    }
}
