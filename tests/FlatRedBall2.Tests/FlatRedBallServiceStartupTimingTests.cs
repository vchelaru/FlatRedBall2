using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class FlatRedBallServiceStartupTimingTests
{
    private class TestScreen : Screen { }

    [Fact]
    public void Start_WithStartupTimingDisabled_EmitsNothing()
    {
        var service = new FlatRedBallService();
        var emitted = new List<string>();
        service.StartupReportWriter = emitted.Add;

        service.Start<TestScreen>();

        // Off by default: an unprofiled boot must not pay for, or print, a report.
        emitted.ShouldBeEmpty();
        service.StartupTiming.HasRecordedPhases.ShouldBeFalse();
    }

    [Fact]
    public void Start_WithStartupTimingEnabled_EmitsReportContainingScreenLoadPhase()
    {
        var service = new FlatRedBallService();
        var emitted = new List<string>();
        service.StartupReportWriter = emitted.Add;
        service.StartupTiming.IsEnabled = true;

        service.Start<TestScreen>();

        emitted.Count.ShouldBe(1);
        emitted[0].ShouldContain("Startup timing");
        emitted[0].ShouldContain("Screen load");
    }

    [Fact]
    public void Start_WithStartupTimingEnabled_EmitsReportOnlyForTheFirstScreen()
    {
        var service = new FlatRedBallService();
        var emitted = new List<string>();
        service.StartupReportWriter = emitted.Add;
        service.StartupTiming.IsEnabled = true;

        service.Start<TestScreen>();
        service.Start<TestScreen>();

        // The report is a load-time breakdown, not a per-screen-transition log — a mid-game screen
        // change must not print a second one.
        emitted.Count.ShouldBe(1);
    }
}
