namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Which frames one playback advance entered, shared by every player that raises
/// <see cref="FlatRedBall2.Animation.AnimationFrameEvent"/>s so they agree on skipped-frame and
/// loop semantics.
/// </summary>
internal static class FrameEventRange
{
    /// <summary>
    /// Returns the entered frames as a run in cyclic playback order: frame
    /// <c>(Start + j) % frameCount</c> for <c>j</c> in <c>[0, Length)</c>. Every frame crossed is
    /// included (a large delta skipping frames still enters them), and the run always ends on
    /// <paramref name="newIndex"/>. An advance covering a full loop or more yields each frame once.
    /// </summary>
    /// <param name="wraps">How many times the advance wrapped past the chain's end.</param>
    public static (int Start, int Length) Get(int previousIndex, int newIndex, int wraps, int frameCount)
    {
        if (wraps == 0)
            return (previousIndex + 1, System.Math.Max(0, newIndex - previousIndex));
        if (wraps == 1 && newIndex < previousIndex)
            return (previousIndex + 1, frameCount - previousIndex + newIndex);
        return (newIndex + 1, frameCount);
    }
}
