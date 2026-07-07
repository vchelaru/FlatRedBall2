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
    public void Scale_GrowsThenSettles_NeverBelowActualSize()
    {
        // Grow-and-settle: the box starts large and eases monotonically down to its real size,
        // never dipping under it (no undershoot that would make it smaller than the region).
        float prev = RevealAnimation.Scale(0f);
        for (int i = 1; i <= 100; i++)
        {
            float s = RevealAnimation.Scale(i / 100f);
            Assert.True(s >= 1f - 1e-4f, $"scale dipped below final size at t={i / 100f}: {s}");
            Assert.True(s <= prev + 1e-4f, $"scale grew instead of settling at t={i / 100f}");
            prev = s;
        }
    }
}
