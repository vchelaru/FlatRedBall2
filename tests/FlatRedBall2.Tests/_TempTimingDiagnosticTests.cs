using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace FlatRedBall2.Tests;

// TEMP diagnostic — throwaway, not meant to land on main. Measures the real cost of engine
// construction on the ubuntu CI runner, to compare against local Windows numbers (sub-ms/call)
// without waiting on the full 15-minute suite.
public class _TempTimingDiagnosticTests
{
    private readonly ITestOutputHelper _output;
    public _TempTimingDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Time_FlatRedBallServiceConstruction_Repeated()
    {
        _ = new FlatRedBallService();

        const int N = 50;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++)
        {
            _ = new FlatRedBallService();
        }
        sw.Stop();

        _output.WriteLine($"new FlatRedBallService() x{N}: total={sw.ElapsedMilliseconds}ms avg={sw.ElapsedMilliseconds / (double)N:F2}ms/call");
    }

    [Fact]
    public void Time_DetectSourceContentRoots_Repeated()
    {
        _ = FlatRedBallService.DetectSourceContentRoots(AppContext.BaseDirectory);

        const int N = 50;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++)
        {
            _ = FlatRedBallService.DetectSourceContentRoots(AppContext.BaseDirectory);
        }
        sw.Stop();

        _output.WriteLine($"DetectSourceContentRoots x{N}: total={sw.ElapsedMilliseconds}ms avg={sw.ElapsedMilliseconds / (double)N:F2}ms/call");
    }

    [Fact]
    public void Time_EngineStart_Repeated()
    {
        var engine = new FlatRedBallService();
        engine.Start<TempTimingScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        const int N = 20;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++)
        {
            var e = new FlatRedBallService();
            e.Start<TempTimingScreen>();
            e.Update(new Microsoft.Xna.Framework.GameTime());
        }
        sw.Stop();

        _output.WriteLine($"new+Start+Update x{N}: total={sw.ElapsedMilliseconds}ms avg={sw.ElapsedMilliseconds / (double)N:F2}ms/call");
    }

    private class TempTimingScreen : Screen { }
}
