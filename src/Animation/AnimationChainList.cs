using System.Collections.Generic;

namespace FlatRedBall2.Animation;

/// <summary>
/// A named collection of <see cref="AnimationChain"/>s. Assign to
/// <see cref="FlatRedBall2.Rendering.Sprite.AnimationChains"/>.
/// Supports lookup by chain name via the string indexer.
/// </summary>
public class AnimationChainList : List<AnimationChain>
{
    /// <summary>Optional identifier for this list — typically the source file name (e.g. <c>"Player.achx"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Returns the chain whose <see cref="AnimationChain.Name"/> matches <paramref name="name"/>,
    /// or <c>null</c> if no chain with that name exists. Linear scan — O(n) in the chain count;
    /// fine for typical animation lists (rarely more than a handful of chains per entity).
    /// </summary>
    public AnimationChain? this[string name]
    {
        get
        {
            foreach (var chain in this)
                if (chain.Name == name) return chain;
            return null;
        }
    }
}
