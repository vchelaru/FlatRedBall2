using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.GoldenImage.Tests;

public class GoldenComparerTests
{
    private static PixelImage Solid(int width, int height, Color color)
    {
        var pixels = new Color[width * height];
        System.Array.Fill(pixels, color);
        return new PixelImage(width, height, pixels);
    }

    [Fact]
    public void Compare_IdenticalImages_Matches()
    {
        var result = GoldenComparer.Compare(Solid(2, 2, new Color(10, 20, 30, 255)), Solid(2, 2, new Color(10, 20, 30, 255)), tolerance: 0);

        result.Outcome.ShouldBe(ComparisonOutcome.Match);
        result.MismatchedPixelCount.ShouldBe(0);
        result.MaxChannelDelta.ShouldBe(0);
    }

    [Fact]
    public void Compare_DeltaWithinTolerance_Matches()
    {
        var expected = Solid(1, 1, new Color(100, 100, 100, 255));
        var actual = Solid(1, 1, new Color(102, 98, 100, 255));

        var result = GoldenComparer.Compare(expected, actual, tolerance: 2);

        result.Outcome.ShouldBe(ComparisonOutcome.Match);
        result.MaxChannelDelta.ShouldBe(2);
    }

    [Theory]
    [InlineData(0)] // red
    [InlineData(1)] // green
    [InlineData(2)] // blue
    [InlineData(3)] // alpha
    public void Compare_OneChannelBeyondTolerance_Mismatches(int channel)
    {
        var expected = Solid(1, 1, new Color(100, 100, 100, 255));
        var off = new[] { 100, 100, 100, 255 };
        off[channel] += channel == 3 ? -3 : 3;
        var actual = Solid(1, 1, new Color(off[0], off[1], off[2], off[3]));

        var result = GoldenComparer.Compare(expected, actual, tolerance: 2);

        result.Outcome.ShouldBe(ComparisonOutcome.PixelMismatch);
        result.MismatchedPixelCount.ShouldBe(1);
        result.MaxChannelDelta.ShouldBe(3);
    }

    [Fact]
    public void Compare_SizeMismatch_ReportsBothSizesAndNoDiff()
    {
        var result = GoldenComparer.Compare(Solid(2, 2, Color.White), Solid(3, 2, Color.White), tolerance: 0);

        result.Outcome.ShouldBe(ComparisonOutcome.SizeMismatch);
        result.Diff.ShouldBeNull();
        result.Summary.ShouldContain("2x2");
        result.Summary.ShouldContain("3x2");
    }

    [Fact]
    public void Compare_Mismatch_DiffMarksOnlyDifferingPixelsMagenta()
    {
        var expected = Solid(2, 1, new Color(40, 40, 40, 255));
        var actual = new PixelImage(2, 1, new[] { new Color(40, 40, 40, 255), new Color(200, 40, 40, 255) });

        var result = GoldenComparer.Compare(expected, actual, tolerance: 0);

        result.Diff.ShouldNotBeNull();
        result.Diff![0, 0].ShouldNotBe(new Color(255, 0, 255, 255));
        result.Diff[1, 0].ShouldBe(new Color(255, 0, 255, 255));
        result.MismatchedPixelCount.ShouldBe(1);
    }
}
