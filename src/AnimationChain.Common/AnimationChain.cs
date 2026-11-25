using System;
using System.Collections.Generic;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A named sequence of <typeparamref name="TFrame"/>s. Assign to an
/// <see cref="AnimationPlayer{TFrame}"/> and play via <see cref="AnimationPlayer{TFrame}.Play(string)"/>.
/// </summary>
public class AnimationChain<TFrame> : List<TFrame>, IReloadableChain where TFrame : AnimationFrameBase
{
    /// <summary>
    /// Identifier used by <see cref="AnimationPlayer{TFrame}.Play(string)"/> and by the
    /// <see cref="AnimationChainList{TFrame}"/> string indexer to look this chain up.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this chain loops by default. <see cref="AnimationPlayer{TFrame}.IsLooping"/> follows
    /// this value while the chain plays, unless it is set on the player.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    /// Whether this chain plays forward and then mirrored by default (see
    /// <see cref="FlatRedBall2.AnimationEditorCommon.AnimationChainSave.PingPong"/>).
    /// <see cref="AnimationPlayer{TFrame}.IsPingPong"/> follows it like <see cref="Loop"/>.
    /// </summary>
    public bool PingPong { get; set; }

    /// <summary>
    /// Whether this chain plays its timeline mirrored (backward) by default (see
    /// <see cref="FlatRedBall2.AnimationEditorCommon.AnimationChainSave.Reverse"/>).
    /// <see cref="AnimationPlayer{TFrame}.IsReversed"/> follows it like <see cref="Loop"/>.
    /// </summary>
    public bool Reversed { get; set; }

    /// <summary>Total duration of the animation (sum of all frame lengths).</summary>
    public TimeSpan TotalLength
    {
        get
        {
            var sum = TimeSpan.Zero;
            foreach (var frame in this)
                sum += frame.FrameLength;
            return sum;
        }
    }
}
