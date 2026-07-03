using AnimationEditor.Core.CommandsAndState;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TickClockTests
{
    // A tick source where 1000 ticks == 1 second, so elapsed math is easy to read.
    private const long Frequency = 1000;

    [Fact]
    public void Tick_FirstCall_ReturnsZero()
    {
        long now = 500;
        var clock = new TickClock(() => now, Frequency);
        Assert.Equal(0.0, clock.Tick());
    }

    [Fact]
    public void Tick_SubsequentCalls_ReturnRealElapsedSeconds()
    {
        // Timestamps advance by an *uneven* amount each tick — 20 ms then 30 ms —
        // proving the clock reports true elapsed time rather than a fixed step.
        long now = 0;
        var clock = new TickClock(() => now, Frequency);

        clock.Tick();          // establishes baseline at 0, returns 0
        now = 20;              // 20 ticks == 0.020 s
        Assert.Equal(0.020, clock.Tick(), 6);
        now = 50;              // +30 ticks == 0.030 s
        Assert.Equal(0.030, clock.Tick(), 6);
    }

    [Fact]
    public void Tick_AfterReset_ReturnsZeroThenResumesFromNewBaseline()
    {
        long now = 100;
        var clock = new TickClock(() => now, Frequency);
        clock.Tick();          // baseline at 100
        now = 200;
        clock.Tick();          // 0.100 s

        clock.Reset();
        now = 1000;            // large gap while "paused"
        Assert.Equal(0.0, clock.Tick());   // discarded, no jump
        now = 1010;
        Assert.Equal(0.010, clock.Tick(), 6);
    }
}
