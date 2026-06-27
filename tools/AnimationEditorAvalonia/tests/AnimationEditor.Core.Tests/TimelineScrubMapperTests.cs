using AnimationEditor.Core.ViewModels;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TimelineScrubMapperTests
{
    // Three equal 24px cells laid out at [0,24), [24,48), [48,72).
    private static readonly double[] ThreeCells = { 24.0, 24.0, 24.0 };

    [Fact]
    public void Resolve_EmptyCells_ReturnsFrameZero()
    {
        var result = TimelineScrubMapper.Resolve(50, System.Array.Empty<double>());

        Assert.Equal(0, result.FrameIndex);
    }

    [Fact]
    public void Resolve_NegativeX_ClampsToStartOfFirstFrame()
    {
        var result = TimelineScrubMapper.Resolve(-10, ThreeCells);

        Assert.Equal(0, result.FrameIndex);
        Assert.Equal(0.0, result.LocalX, precision: 6);
        Assert.Equal(0.0, result.Fraction, precision: 6);
    }

    [Fact]
    public void Resolve_XBeyondEnd_ClampsToEndOfLastFrame()
    {
        var result = TimelineScrubMapper.Resolve(1000, ThreeCells);

        Assert.Equal(2, result.FrameIndex);
        Assert.Equal(24.0, result.LocalX, precision: 6);
        Assert.Equal(1.0, result.Fraction, precision: 6);
    }

    [Fact]
    public void Resolve_XInFirstCell_ReturnsFrameZeroWithLocalX()
    {
        var result = TimelineScrubMapper.Resolve(6, ThreeCells); // 6px into the 24px cell

        Assert.Equal(0, result.FrameIndex);
        Assert.Equal(6.0, result.LocalX, precision: 6);
        Assert.Equal(0.25, result.Fraction, precision: 6);
    }

    [Fact]
    public void Resolve_XInSecondCell_ReturnsFrameOneWithCellRelativeLocalX()
    {
        // x=30 is 6px past the 24px first cell → frame 1, localX 6.
        var result = TimelineScrubMapper.Resolve(30, ThreeCells);

        Assert.Equal(1, result.FrameIndex);
        Assert.Equal(6.0, result.LocalX, precision: 6);
        Assert.Equal(0.25, result.Fraction, precision: 6);
    }
}
