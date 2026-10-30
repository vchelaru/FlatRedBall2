using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationChain.Common.Tests;

public class FrameFlipTransformTests
{
    [Theory]
    [InlineData(false, false, false, false, 0f, false, false)]
    [InlineData(true, false, false, false, 0f, true, false)]
    [InlineData(false, true, false, false, 0f, false, true)]
    [InlineData(false, false, false, true, 0f, false, true)]  // camera Y-flip supplies the vertical mirror
    [InlineData(false, true, false, true, 0f, false, false)]  // user flip cancels it
    public void Compute_NoDiagonal(bool h, bool v, bool d, bool flipsY, float rot, bool expectH, bool expectV)
    {
        var result = FrameFlipTransform.Compute(h, v, d, flipsY);

        Assert.Equal(rot, result.RotationOffsetRadians);
        Assert.Equal(expectH, result.FlipHorizontally);
        Assert.Equal(expectV, result.FlipVertically);
    }

    [Fact]
    public void Compute_DiagonalOnly_ScreenSpace_RotatesPositiveQuarterAndFlipsVertically()
    {
        var result = FrameFlipTransform.Compute(false, false, true, flipsY: false);

        Assert.Equal(MathF.PI / 2f, result.RotationOffsetRadians);
        Assert.False(result.FlipHorizontally);
        Assert.True(result.FlipVertically);
    }

    [Fact]
    public void Compute_DiagonalOnly_YFlipped_RotatesNegativeQuarter()
    {
        var result = FrameFlipTransform.Compute(false, false, true, flipsY: true);

        Assert.Equal(-MathF.PI / 2f, result.RotationOffsetRadians);
        Assert.False(result.FlipHorizontally);
        Assert.False(result.FlipVertically);
    }
}
