using AnimationEditor.Core.Export.Gif;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class GifTimingTests
{
    [Fact]
    public void PlanDelays_50Fps_IsTwoCentisecondsPerFrame()
    {
        int[] delays = GifTiming.PlanDelays(durationSeconds: 1.0, frameRate: 50);

        delays.Length.ShouldBe(50);
        delays.ShouldAllBe(d => d == 2);
    }

    [Fact]
    public void PlanDelays_30Fps_AlternatesThreeAndFourSoTheTotalStaysExact()
    {
        int[] delays = GifTiming.PlanDelays(durationSeconds: 1.0, frameRate: 30);

        delays.Length.ShouldBe(30);
        delays.ShouldAllBe(d => d == 3 || d == 4);
        delays.Sum().ShouldBe(100);
    }

    [Fact]
    public void PlanDelays_DurationNotOnTheFrameGrid_FoldsASubMinimumTailIntoThePreviousFrame()
    {
        // 0.45s at 50fps is 22.5 steps; a 1cs tail frame would play at 10cs in browsers, so it is dropped.
        int[] delays = GifTiming.PlanDelays(durationSeconds: 0.45, frameRate: 50);

        delays.Sum().ShouldBe(45);
        delays.ShouldAllBe(d => d >= 2);
    }

    [Fact]
    public void PlanDelays_VeryShortDuration_StillProducesOneFrameOfAtLeastTwoCentiseconds()
    {
        int[] delays = GifTiming.PlanDelays(durationSeconds: 0.001, frameRate: 50);

        delays.ShouldBe(new[] { 2 });
    }
}
