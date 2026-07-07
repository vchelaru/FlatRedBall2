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
    public void Scale_OverTheReveal_BouncesMultipleTimes()
    {
        // A double bounce crosses its final size (1.0) several times as it oscillates and settles;
        // a single-overshoot settle would cross only once. Count sign changes of (scale − 1).
        int crossings = 0;
        int prevSign = System.Math.Sign(RevealAnimation.Scale(0f) - 1f);
        for (int i = 1; i <= 100; i++)
        {
            int sign = System.Math.Sign(RevealAnimation.Scale(i / 100f) - 1f);
            if (sign != 0 && sign != prevSign) { crossings++; prevSign = sign; }
        }

        Assert.True(crossings >= 3, $"expected multiple bounces, got {crossings} crossings of 1.0");
    }
}
