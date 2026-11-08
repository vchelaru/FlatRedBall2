using System;
using System.Collections.Concurrent;
using AnimationEditor.Core.Logging;
using Xunit;

namespace AnimationEditor.Core.Tests.Logging;

public class LingerWatchdogTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public void Arm_ReportsReasonAndSnapshotOnceTheDelayPasses()
    {
        var reports = new BlockingCollection<string>();
        using var watchdog = new LingerWatchdog(Short, Short, maxReports: 1, reports.Add, () => "SNAPSHOT");

        watchdog.Arm("last window closed");

        Assert.True(reports.TryTake(out var report, Wait));
        Assert.Contains("last window closed", report);
        Assert.Contains("SNAPSHOT", report);
    }

    [Fact]
    public void Arm_Twice_RunsOnlyOneSchedule()
    {
        var reports = new BlockingCollection<string>();
        using var watchdog = new LingerWatchdog(Short, Short, maxReports: 2, reports.Add, () => "s");

        watchdog.Arm("first");
        watchdog.Arm("second");

        Assert.True(reports.TryTake(out var a, Wait));
        Assert.True(reports.TryTake(out var b, Wait));
        Assert.False(reports.TryTake(out _, TimeSpan.FromMilliseconds(300)));
        Assert.Contains("first", a);
        Assert.Contains("first", b);
    }

    [Fact]
    public void Dispose_BeforeTheDelay_ReportsNothing()
    {
        var reports = new BlockingCollection<string>();
        var watchdog = new LingerWatchdog(TimeSpan.FromMilliseconds(300), Short, maxReports: 3, reports.Add, () => "s");
        watchdog.Arm("closing");

        watchdog.Dispose();

        Assert.False(reports.TryTake(out _, TimeSpan.FromMilliseconds(700)));
    }

    [Fact]
    public void ASnapshotThatThrows_IsReportedInsteadOfLost()
    {
        var reports = new BlockingCollection<string>();
        using var watchdog = new LingerWatchdog(Short, Short, maxReports: 1, reports.Add,
            () => throw new InvalidOperationException("snapshot-boom"));

        watchdog.Arm("closing");

        Assert.True(reports.TryTake(out var report, Wait));
        Assert.Contains("snapshot-boom", report);
    }

    [Fact]
    public void Capture_DescribesTheCurrentProcess()
    {
        var text = ProcessSnapshot.Capture();

        Assert.Contains($"pid={Environment.ProcessId}", text);
        Assert.Contains("threads=", text);
        Assert.Contains("threadPool", text);
    }
}
