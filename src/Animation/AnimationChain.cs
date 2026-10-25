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

    /// <summary>
    /// Whether this chain loops by default. <see cref="FlatRedBall2.Rendering.Sprite.PlayAnimation(string)"/>
    /// seeds <see cref="FlatRedBall2.Rendering.Sprite.IsLooping"/> from this value when the chain
    /// starts playing, but that flag stays overridable per-instance afterward.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    /// Whether this chain bounces back and forth by default (A B C B A...; see
    /// <see cref="FlatRedBall2.AnimationEditorCommon.AnimationChainSave.PingPong"/>). Seeds
    /// <see cref="FlatRedBall2.Rendering.Sprite.IsPingPong"/> when the chain starts playing; overridable per instance afterward.
    /// </summary>
    public bool PingPong { get; set; }

    /// <summary>
    /// Whether this chain plays backward by default, starting on its last frame (see
    /// <see cref="FlatRedBall2.AnimationEditorCommon.AnimationChainSave.Reverse"/>). Seeds
    /// <see cref="FlatRedBall2.Rendering.Sprite.IsReversed"/> when the chain starts playing; overridable per instance afterward.
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
