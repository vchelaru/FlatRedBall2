using System;
using System.Collections.Generic;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A named sequence of <typeparamref name="TFrame"/>s. Assign to an
/// <see cref="AnimationPlayer{TFrame}"/> and play via <see cref="AnimationPlayer{TFrame}.Play(string)"/>.
/// </summary>
public class AnimationChain<TFrame> : List<TFrame> where TFrame : AnimationFrameBase
{
    /// <summary>
    /// Identifier used by <see cref="AnimationPlayer{TFrame}.Play(string)"/> and by the
    /// <see cref="AnimationChainList{TFrame}"/> string indexer to look this chain up.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this chain loops by default. <see cref="AnimationPlayer{TFrame}"/> seeds its own
    /// <see cref="AnimationPlayer{TFrame}.IsLooping"/> from this value when the chain starts
    /// playing, but that flag stays overridable per-instance afterward.
    /// </summary>
    public bool Loop { get; set; } = true;

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
