using System;
using System.Collections.Generic;
using FlatRedBall2.AnimationEditorCommon;

namespace FlatRedBall2.Animation;

/// <summary>
/// A named sequence of <see cref="AnimationFrame"/>s. Assign to
/// <see cref="FlatRedBall2.Rendering.Sprite.AnimationChains"/> and play via
/// <see cref="FlatRedBall2.Rendering.Sprite.PlayAnimation(string)"/>.
/// </summary>
public class AnimationChain : List<AnimationFrame>, IReloadableChain
{
    /// <summary>
    /// Identifier used by <see cref="FlatRedBall2.Rendering.Sprite.PlayAnimation(string)"/> and by
    /// the <see cref="AnimationChainList"/> string indexer to look this chain up.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this chain loops by default. <see cref="FlatRedBall2.Rendering.Sprite.IsLooping"/> follows
    /// this value while the chain plays, unless it is set on the sprite.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    /// Whether this chain plays forward and then mirrored by default (see
    /// <see cref="FlatRedBall2.AnimationEditorCommon.AnimationChainSave.PingPong"/>).
    /// <see cref="FlatRedBall2.Rendering.Sprite.IsPingPong"/> follows it like <see cref="Loop"/>.
    /// </summary>
    public bool PingPong { get; set; }

    /// <summary>
    /// Whether this chain plays its timeline mirrored (backward) by default (see
    /// <see cref="FlatRedBall2.AnimationEditorCommon.AnimationChainSave.Reverse"/>).
    /// <see cref="FlatRedBall2.Rendering.Sprite.IsReversed"/> follows it like <see cref="Loop"/>.
    /// </summary>
    public bool Reversed { get; set; }

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
