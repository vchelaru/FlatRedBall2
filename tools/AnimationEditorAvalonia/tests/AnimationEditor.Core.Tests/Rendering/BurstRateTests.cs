using AnimationEditor.Core.Rendering;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Rendering;

public class BurstRateTests
{
    [Fact]
    public void Hertz_SteadyEvents_IsTheirRate()
    {
        var rate = new BurstRate();

        for (int i = 0; i < 20; i++) rate.Add(i * 0.02);

        rate.Hertz.ShouldBe(50, 1e-6);
    }

    [Fact]
    public void Hertz_ABurstFollowedByIdleTime_StillReadsTheBurstRate()
    {
        var rate = new BurstRate();
        for (int i = 0; i < 10; i++) rate.Add(i * 0.02);

        // The idle second is not part of any burst, so it must not drag the rate down.
        rate.Add(2.0);

        rate.Hertz.ShouldBe(50, 1e-6);
    }

    [Fact]
    public void Hertz_ASecondBurstReplacesTheFirstOnceItHasEnoughEvents()
    {
        var rate = new BurstRate();
        for (int i = 0; i < 10; i++) rate.Add(i * 0.02);

        for (int i = 0; i < 6; i++) rate.Add(5 + i * 0.04);

        rate.Hertz.ShouldBe(25, 1e-6);
    }

    [Fact]
    public void WorstGapMilliseconds_IsTheLongestStallInTheBurst()
    {
        var rate = new BurstRate();
        rate.Add(0);
        rate.Add(0.016);
        rate.Add(0.032);
        rate.Add(0.100);
        rate.Add(0.116);

        rate.WorstGapMilliseconds.ShouldBe(68, 1e-6);
    }
}
