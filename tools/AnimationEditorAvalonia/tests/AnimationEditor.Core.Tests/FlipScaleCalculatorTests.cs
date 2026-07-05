using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

// ── FlipScaleCalculator ───────────────────────────────────────────────────────
// Pure tests — no collection attribute needed (no singletons touched).

public class FlipScaleCalculatorTests
{
    // ComputeMatrix — (x, y) -> (a*x + b*y, c*x + d*y). Diagonal alone maps (x,y) -> (-y,-x),
    // matching TileMapCollisions.ApplyFlips' diagonal-flip transpose; H/V then mirror on top. ——

    [Theory]
    [InlineData(false, false, false,  1f,  0f,  0f,  1f)]
    [InlineData(true,  false, false, -1f,  0f,  0f,  1f)]
    [InlineData(false, true,  false,  1f,  0f,  0f, -1f)]
    [InlineData(false, false, true,   0f, -1f, -1f,  0f)] // (x,y) -> (-y,-x)
    [InlineData(true,  false, true,   0f,  1f, -1f,  0f)]
    [InlineData(false, true,  true,   0f, -1f,  1f,  0f)]
    [InlineData(true,  true,  true,   0f,  1f,  1f,  0f)]
    public void ComputeMatrix_Theory(bool flipH, bool flipV, bool flipD, float a, float b, float c, float d)
    {
        var m = FlipScaleCalculator.ComputeMatrix(flipH, flipV, flipD);
        Assert.Equal(a, m.a);
        Assert.Equal(b, m.b);
        Assert.Equal(c, m.c);
        Assert.Equal(d, m.d);
    }

    // IsFlipped ———————————————————————————————————————————————————————————————

    [Fact]
    public void IsFlipped_NoFlags_ReturnsFalse()
        => Assert.False(FlipScaleCalculator.IsFlipped(false, false));

    [Fact]
    public void IsFlipped_FlipH_ReturnsTrue()
        => Assert.True(FlipScaleCalculator.IsFlipped(true, false));

    [Fact]
    public void IsFlipped_FlipV_ReturnsTrue()
        => Assert.True(FlipScaleCalculator.IsFlipped(false, true));

    [Fact]
    public void IsFlipped_FlipDiagonalOnly_ReturnsTrue()
        => Assert.True(FlipScaleCalculator.IsFlipped(false, false, flipDiagonal: true));

    [Fact]
    public void IsFlipped_BothSet_ReturnsTrue()
        => Assert.True(FlipScaleCalculator.IsFlipped(true, true));
}
