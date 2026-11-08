using System;
using System.IO;
using AnimationEditor.Core.Logging;
using Xunit;

namespace AnimationEditor.Core.Tests.Logging;

public class CleanShutdownMarkerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"frb-marker-{Guid.NewGuid():N}");
    private string MarkerPath => Path.Combine(_dir, "running.marker");
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Begin_WithNoMarker_ReturnsNullAndWritesOne()
    {
        var previous = new CleanShutdownMarker(MarkerPath).Begin(processId: 10, version: "1.0", Start);

        Assert.Null(previous);
        Assert.True(File.Exists(MarkerPath));
    }

    [Fact]
    public void Begin_AfterARunThatNeverMarkedClean_ReportsThatRun()
    {
        new CleanShutdownMarker(MarkerPath).Begin(processId: 10, version: "1.0", Start);

        var previous = new CleanShutdownMarker(MarkerPath).Begin(processId: 11, version: "1.1", Start.AddHours(1));

        Assert.NotNull(previous);
        Assert.Equal(10, previous!.ProcessId);
        Assert.Equal("1.0", previous.Version);
        Assert.Equal(Start, previous.Started);
        Assert.Equal(CleanShutdownMarker.RunningPhase, previous.Phase);
    }

    [Fact]
    public void Begin_AfterMarkClean_ReportsNothing()
    {
        var first = new CleanShutdownMarker(MarkerPath);
        first.Begin(processId: 10, version: "1.0", Start);
        first.MarkClean();

        Assert.Null(new CleanShutdownMarker(MarkerPath).Begin(processId: 11, version: "1.1", Start.AddHours(1)));
    }

    [Fact]
    public void SetPhase_IsVisibleToTheNextRun()
    {
        var first = new CleanShutdownMarker(MarkerPath);
        first.Begin(processId: 10, version: "1.0", Start);
        first.SetPhase(CleanShutdownMarker.UpdatingPhase);

        var previous = new CleanShutdownMarker(MarkerPath).Begin(processId: 11, version: "1.1", Start.AddHours(1));

        Assert.Equal(CleanShutdownMarker.UpdatingPhase, previous!.Phase);
        Assert.Equal(10, previous.ProcessId);
    }

    [Fact]
    public void Begin_WithAGarbledMarker_StillReportsAnUncleanRun()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(MarkerPath, "not a marker");

        var previous = new CleanShutdownMarker(MarkerPath).Begin(processId: 11, version: "1.1", Start);

        Assert.NotNull(previous);
        Assert.Null(previous!.ProcessId);
    }

    [Fact]
    public void Begin_WhenThePathCannotBeWritten_DoesNotThrow()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "file");
        File.WriteAllText(blocker, "x");
        var marker = new CleanShutdownMarker(Path.Combine(blocker, "sub", "running.marker"));

        Assert.Null(marker.Begin(processId: 1, version: "1.0", Start));
        marker.SetPhase(CleanShutdownMarker.UpdatingPhase);
        marker.MarkClean();
    }

    [Fact]
    public void Describe_NamesTheProcessVersionStartTimeAndPhase()
    {
        var text = new CleanShutdownMarker.PreviousRun(10, "1.0", Start, CleanShutdownMarker.RunningPhase).Describe();

        Assert.Contains("pid=10", text);
        Assert.Contains("v1.0", text);
        Assert.Contains("2026-11-08", text);
        Assert.Contains(CleanShutdownMarker.RunningPhase, text);
    }
}
