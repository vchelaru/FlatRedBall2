using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class RevealAnimationTests
{
    [Fact]
    public void Scale_ProgressZero_StartsLargerThanActualSize()
    {
        Assert.True(RevealAnimation.Scale(0f) > 1f);
    }

    [Fact]
    public void Scale_ProgressOne_SettlesToActualSize()
    {
        Assert.Equal(1f, RevealAnimation.Scale(1f), 3);
    }

    [Fact]
    public void Scale_NearEnd_OvershootsBelowActualSize()
    {
        // The back-ease overshoots: the box dips a hair under its real size before settling,
        // which reads as a subtle bounce.
        Assert.True(RevealAnimation.Scale(0.8f) < 1f);
    }
}
