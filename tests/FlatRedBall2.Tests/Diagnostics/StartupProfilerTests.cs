using System.Diagnostics;
using FlatRedBall2.Diagnostics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Diagnostics;

public class StartupProfilerTests
{
    /// <summary>
    /// Stands in for <see cref="Stopwatch.GetTimestamp"/> so a phase can be given an exact
    /// duration — real elapsed time would make every assertion below a race.
    /// </summary>
    private sealed class FakeClock
    {
        private long _ticks;

        public long Now() => _ticks;

        public void AdvanceMs(double ms) => _ticks += (long)(ms * Stopwatch.Frequency / 1000.0);
    }

    private static StartupProfiler CreateProfiler(FakeClock clock) =>
        new() { IsEnabled = true, TimestampProvider = clock.Now };

    [Fact]
    public void BeginPhase_WhenDisabled_RecordsNothing()
    {
        var clock = new FakeClock();
        var profiler = new StartupProfiler { IsEnabled = false, TimestampProvider = clock.Now };

        profiler.BeginPhase("Engine.Initialize");
        clock.AdvanceMs(500);
        profiler.EndPhase();

        profiler.HasRecordedPhases.ShouldBeFalse();
        profiler.GenerateReport().ShouldBeEmpty();
    }

    [Fact]
    public void EndPhase_MoreCallsThanBeginPhase_DoesNotThrow()
    {
        var clock = new FakeClock();
        var profiler = CreateProfiler(clock);

        profiler.BeginPhase("Engine.Initialize");
        profiler.EndPhase();

        // An early return or exception on a boot path can leave EndPhase unbalanced. Losing the
        // timing is acceptable; taking the game down over a diagnostic is not.
        Should.NotThrow(() => profiler.EndPhase());
    }

    [Fact]
    public void GenerateReport_NestedPhases_IndentsChildUnderParentWithPercentages()
    {
        var clock = new FakeClock();
        var profiler = CreateProfiler(clock);

        // Engine.Initialize spans 1000ms, of which the nested Gum project load is 750ms (75%).
        profiler.BeginPhase("Engine.Initialize");
        profiler.BeginPhase("Gum project load");
        clock.AdvanceMs(750);
        profiler.EndPhase();
        clock.AdvanceMs(250);
        profiler.EndPhase();

        var report = profiler.GenerateReport();

        report.ShouldContain("total 1000.00ms");
        report.ShouldContain("  Engine.Initialize");
        report.ShouldContain("    Gum project load");
        report.ShouldContain("750.00ms");
        report.ShouldContain("75.0%");
    }

    [Fact]
    public void GenerateReport_SiblingPhases_ListsBothInBeginOrder()
    {
        var clock = new FakeClock();
        var profiler = CreateProfiler(clock);

        profiler.BeginPhase("Gum project load");
        clock.AdvanceMs(300);
        profiler.EndPhase();
        profiler.BeginPhase("Screen load");
        clock.AdvanceMs(100);
        profiler.EndPhase();

        var report = profiler.GenerateReport();

        report.ShouldContain("total 400.00ms");
        report.IndexOf("Gum project load").ShouldBeLessThan(report.IndexOf("Screen load"));
    }

    [Fact]
    public void GenerateReport_UnclosedPhase_MarksPhaseIncomplete()
    {
        var clock = new FakeClock();
        var profiler = CreateProfiler(clock);

        profiler.BeginPhase("Engine.Initialize");
        clock.AdvanceMs(200);

        // Report generated while the phase is still open — it must say so rather than print a
        // duration that is really "time until someone asked for the report".
        profiler.GenerateReport().ShouldContain("incomplete");
    }
}
