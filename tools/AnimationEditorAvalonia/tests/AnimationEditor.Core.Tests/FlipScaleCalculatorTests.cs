using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

// ── FlipScaleCalculator ───────────────────────────────────────────────────────
// Pure tests — no collection attribute needed (no singletons touched).

public class FlipScaleCalculatorTests
{
    // ComputeMatrix — (x, y) -> (a*x + b*y, c*x + d*y). This matrix applies directly to
    // SkiaSharp canvas coordinates, which are Y-down (origin top-left, no up-conversion) —
    // unlike TileMapCollisions.ApplyFlips, which converts Tiled's Y-down pixel data to FRB2's
    // Y-up local space *before* applying its diagonal step. "Reflect across the diagonal" has
    // opposite signs in those two conventions: in Y-down space, diagonal-only is the plain swap
    // (x,y) -> (y,x) — matrix (0,1,1,0) — which fixes the top-left/bottom-right corners and
    // swaps top-right/bottom-left, matching Tiled's actual diagonal-flip semantics (verified
    // against TileMapCollisionsTests.GenerateFromClass_PolygonTileFlippedDiagonally_...). H/V
    // then mirror on top, in D -> H -> V order.

    [Theory]
    [InlineData(false, false, false,  1f,  0f,  0f,  1f)]
    [InlineData(true,  false, false, -1f,  0f,  0f,  1f)]
    [InlineData(false, true,  false,  1f,  0f,  0f, -1f)]
    [InlineData(false, false, true,   0f,  1f,  1f,  0f)] // (x,y) -> (y,x)
    [InlineData(true,  false, true,   0f, -1f,  1f,  0f)]
    [InlineData(false, true,  true,   0f,  1f, -1f,  0f)]
    [InlineData(true,  true,  true,   0f, -1f, -1f,  0f)]
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
