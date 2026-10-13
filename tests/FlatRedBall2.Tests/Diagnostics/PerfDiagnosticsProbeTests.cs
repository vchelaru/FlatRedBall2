using System.Collections.Generic;
using System.Text.Json;
using FlatRedBall2.Collision;
using FlatRedBall2.Diagnostics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Diagnostics;

public class PerfDiagnosticsProbeTests
{
    private const string Prefix = "[frb-diag] ";

    private static readonly IReadOnlyList<ICollisionRelationship> NoRelationships =
        System.Array.Empty<ICollisionRelationship>();

    private static FlatRedBallService NewService()
    {
        var service = new FlatRedBallService();
        service.Performance.TimerResolutionMs = 0.1;   // skip the busy-wait clock probe
        return service;
    }

    private static void CommitFrame(FlatRedBallService service, PerfDiagnosticsProbe probe, FrameProfile profile)
    {
        service.Performance.Record(profile, NoRelationships);
        probe.OnTick();
    }

    private static JsonDocument ParseLine(string? line)
    {
        line.ShouldNotBeNull();
        line!.ShouldStartWith(Prefix);
        return JsonDocument.Parse(line.Substring(Prefix.Length));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("?foo=1")]
    [InlineData("?diag=other")]
    [InlineData("?diag=")]
    [InlineData("?diagnostics=perf")]
    [InlineData("?frames=120")]
    public void TryParseQuery_DiagPerfAbsent_ReturnsFalse(string? query)
    {
        PerfDiagnosticsProbe.TryParseQuery(query, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("?diag=perf", 300)]
    [InlineData("diag=perf", 300)]
    [InlineData("?diag=PERF", 300)]
    [InlineData("?foo=1&diag=perf", 300)]
    [InlineData("?diag=perf&frames=120", 120)]
    [InlineData("?frames=120&diag=perf", 120)]
    [InlineData("?diag=perf&frames=abc", 300)]
    [InlineData("?diag=perf&frames=0", 300)]
    [InlineData("?diag=perf&frames=-5", 300)]
    [InlineData("?diag=perf&frames=", 300)]
    [InlineData("?diag=perf&frames=99999999", 100000)]
    public void TryParseQuery_DiagPerfPresent_ReturnsFrameCount(string query, int expectedFrames)
    {
        PerfDiagnosticsProbe.TryParseQuery(query, out var frames).ShouldBeTrue();
        frames.ShouldBe(expectedFrames);
    }

    [Fact]
    public void TryCreateFromQuery_NoDiagParam_ReturnsNullAndEnablesNothing()
    {
        var service = NewService();
        var lines = new List<string>();

        var labelRequested = false;

        var probe = PerfDiagnosticsProbe.TryCreateFromQuery(
            "?level=2", service, () => { labelRequested = true; return "Chrome"; }, lines.Add);

        probe.ShouldBeNull();
        labelRequested.ShouldBeFalse();   // host JS interop must not run when the probe is off
        service.Performance.IsEnabled.ShouldBeFalse();
        service.RenderDiagnostics.IsEnabled.ShouldBeFalse();
        service.Performance.PlatformLabel.ShouldBeNull();
        lines.ShouldBeEmpty();
    }

    [Fact]
    public void TryCreateFromQuery_DiagPerf_EnablesPerformanceAndRenderDiagnosticsAndSetsPlatform()
    {
        var service = NewService();

        var probe = PerfDiagnosticsProbe.TryCreateFromQuery("?diag=perf", service, () => "Chrome 141", _ => { });

        probe.ShouldNotBeNull();
        service.Performance.IsEnabled.ShouldBeTrue();
        service.RenderDiagnostics.IsEnabled.ShouldBeTrue();
        service.Performance.PlatformLabel.ShouldBe("Chrome 141");
    }

    [Fact]
    public void OnTick_NoNewFrameRecorded_DoesNotCountFrames()
    {
        var service = NewService();
        var lines = new List<string>();
        var probe = PerfDiagnosticsProbe.TryCreateFromQuery("?diag=perf&frames=2", service, () => null, lines.Add)!;

        service.Performance.Record(new FrameProfile { FrameTotalMs = 5 }, NoRelationships);
        for (int i = 0; i < 10; i++) probe.OnTick();   // ticks that did not draw must not count as frames

        lines.ShouldBeEmpty();
    }

    [Fact]
    public void OnTick_AfterNFrames_WritesExactlyOnePrefixedLineAndNeverAgain()
    {
        var service = NewService();
        var lines = new List<string>();
        var probe = PerfDiagnosticsProbe.TryCreateFromQuery("?diag=perf&frames=3", service, () => "Firefox", lines.Add)!;

        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 1 });
        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 2 });
        lines.ShouldBeEmpty();

        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 3 });
        lines.Count.ShouldBe(1);
        lines[0].ShouldStartWith(Prefix);

        service.Performance.IsEnabled = true;   // even if something re-enables recording, no second line
        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 4 });
        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 5 });
        lines.Count.ShouldBe(1);
    }

    [Fact]
    public void OnTick_AfterReport_RestoresPreviousEnabledState()
    {
        var service = NewService();
        var probe = PerfDiagnosticsProbe.TryCreateFromQuery("?diag=perf&frames=1", service, () => null, _ => { })!;

        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 1 });

        service.Performance.IsEnabled.ShouldBeFalse();
        service.RenderDiagnostics.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void OnTick_GameAlreadyHadPerformanceEnabled_LeavesItEnabledAfterReport()
    {
        var service = NewService();
        service.Performance.IsEnabled = true;
        var probe = PerfDiagnosticsProbe.TryCreateFromQuery("?diag=perf&frames=1", service, () => null, _ => { })!;

        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 1 });

        service.Performance.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void OnTick_ReportJson_HasExpectedShapeAndStatistics()
    {
        var service = NewService();
        string? line = null;
        string? published = null;
        var probe = PerfDiagnosticsProbe.TryCreateFromQuery(
            "?diag=perf&frames=5", service, () => "Chrome 141", l => line = l, j => published = j)!;

        for (int i = 1; i <= 5; i++)
        {
            CommitFrame(service, probe, new FrameProfile
            {
                DrawTotalMs = i * 10,
                UpdateTotalMs = i,
                FrameTotalMs = i * 11,
                DrawCallCount = 100 + i,
                SpriteCount = 200,
                PrimitiveCount = 400,
                TextureCount = 3,
            });
        }

        using var doc = ParseLine(line);
        published.ShouldBe(line!.Substring(Prefix.Length));
        line.ShouldNotContain("\n");

        var root = doc.RootElement;
        root.GetProperty("platform").GetString().ShouldBe("Chrome 141");
        root.GetProperty("frames").GetInt32().ShouldBe(5);
        root.GetProperty("timerResolutionMs").GetDouble().ShouldBe(0.1, 0.0001);

        // Nearest-rank percentiles over 5 samples: median = 3rd, p95 = 5th.
        var draw = root.GetProperty("drawMs");
        draw.GetProperty("median").GetDouble().ShouldBe(30);
        draw.GetProperty("p95").GetDouble().ShouldBe(50);
        draw.GetProperty("max").GetDouble().ShouldBe(50);
        root.GetProperty("updateMs").GetProperty("median").GetDouble().ShouldBe(3);
        root.GetProperty("frameMs").GetProperty("max").GetDouble().ShouldBe(55);
        root.GetProperty("drawCalls").GetProperty("median").GetDouble().ShouldBe(103);
        root.GetProperty("drawCalls").GetProperty("max").GetDouble().ShouldBe(105);
        root.GetProperty("sprites").GetProperty("median").GetDouble().ShouldBe(200);
        root.GetProperty("primitives").GetProperty("median").GetDouble().ShouldBe(400);
        root.GetProperty("textures").GetProperty("median").GetDouble().ShouldBe(3);
        root.GetProperty("batchBreaks").GetProperty("median").GetDouble().ShouldBe(0);
    }

    [Fact]
    public void OnTick_BatchBreaksThisFrame_AreSampled()
    {
        var service = NewService();
        string? line = null;
        var probe = PerfDiagnosticsProbe.TryCreateFromQuery("?diag=perf&frames=1", service, () => null, l => line = l)!;

        service.RenderDiagnostics.RecordBreak(null!, null!, null, 0f, "a", "b");
        service.RenderDiagnostics.RecordBreak(null!, null!, null, 0f, "b", "c");
        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 1 });

        using var doc = ParseLine(line);
        doc.RootElement.GetProperty("batchBreaks").GetProperty("max").GetDouble().ShouldBe(2);
    }

    [Fact]
    public void OnTick_PlatformLabelNeedsEscaping_ProducesValidJson()
    {
        var service = NewService();
        string? line = null;
        var probe = PerfDiagnosticsProbe.TryCreateFromQuery(
            "?diag=perf&frames=1", service, () => "We\"ird\\ \n label", l => line = l)!;

        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 1 });

        using var doc = ParseLine(line);
        doc.RootElement.GetProperty("platform").GetString().ShouldBe("We\"ird\\ \n label");
    }

    [Fact]
    public void OnTick_NullPlatform_ReportsUnknown()
    {
        var service = NewService();
        string? line = null;
        var probe = PerfDiagnosticsProbe.TryCreateFromQuery("?diag=perf&frames=1", service, () => null, l => line = l)!;

        CommitFrame(service, probe, new FrameProfile { FrameTotalMs = 1 });

        using var doc = ParseLine(line);
        doc.RootElement.GetProperty("platform").GetString().ShouldBe("unknown");
    }
}
