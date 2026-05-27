namespace FlatRedBall.AnimationChain;

/// <summary>
/// A named sequence of <see cref="AnimationFrame"/>s. Assign to an
/// <see cref="AnimationPlayer"/> and play via <see cref="AnimationPlayer.Play(string)"/>.
/// </summary>
public class AnimationChain : List<AnimationFrame>
{
    /// <summary>
    /// Identifier used by <see cref="AnimationPlayer.Play(string)"/> and by the
    /// <see cref="AnimationChainList"/> string indexer to look this chain up.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
