using AnimationEditor.Core.Export;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ExportTimingTests
{
    [Fact]
    public void PlanDelays_60FpsInMilliseconds_AlternatesSixteenAndSeventeenSoTheTotalStaysExact()
    {
        int[] delays = ExportTiming.PlanDelays(durationSeconds: 1.0, frameRate: 60, ticksPerSecond: 1000, minTicks: 10);

        delays.Length.ShouldBe(60);
        delays.ShouldAllBe(d => d == 16 || d == 17);
        delays.Sum().ShouldBe(1000);
    }

    [Fact]
    public void PlanDelays_TailBelowTheMinimum_IsFoldedIntoThePreviousStep()
    {
        // 0.505s at 60fps is 30.3 steps; the 5ms tail is under the 10ms minimum.
        int[] delays = ExportTiming.PlanDelays(durationSeconds: 0.505, frameRate: 60, ticksPerSecond: 1000, minTicks: 10);

        delays.Sum().ShouldBe(505);
        delays.ShouldAllBe(d => d >= 10);
    }
}
