using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineRulerTests
{
    [Theory]
    [InlineData(1200, 0.05)] // 0.05 s * 1200 px/s is exactly 60 px: the boundary counts
    [InlineData(1199, 0.1)]
    [InlineData(600, 0.1)]
    [InlineData(240, 0.25)]
    [InlineData(120, 0.5)]
    [InlineData(60, 1)]
    [InlineData(59, 2)]
    [InlineData(30, 2)]
    [InlineData(12, 5)]
    [InlineData(6, 10)]
    [InlineData(4, 15)]
    [InlineData(2, 30)]
    [InlineData(1, 60)]
    [InlineData(0.5, 120)] // past the largest step the ruler falls back to two minutes
    [InlineData(0.01, 120)]
    public void TickStep_IsTheSmallestStepWithAtLeast60PixelsBetweenLabels(double pixelsPerSecond, double expected)
    {
        TimelineRuler.TickStep(pixelsPerSecond).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.25, 0.25, "0.25s")]
    [InlineData(0.1, 0.05, "0.1s")]
    [InlineData(0.05, 0.05, "0.05s")]
    [InlineData(0.0, 0.5, "0s")]
    [InlineData(0.75, 0.25, "0.75s")]
    [InlineData(3.0, 1, "3s")]
    [InlineData(2.4, 1, "2s")] // a whole-second step shows whole seconds, rounded
    [InlineData(10.0, 5, "10s")]
    [InlineData(120.0, 60, "120s")]
    public void FormatTick_ShowsDecimalsUnderOneSecondSteps_AndWholeSecondsFromThere(double time, double step, string expected)
    {
        TimelineRuler.FormatTick(time, step).ShouldBe(expected);
    }

    [Fact]
    public void FormatTick_DoesNotDependOnTheCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            TimelineRuler.FormatTick(0.25, 0.25).ShouldBe("0.25s");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
