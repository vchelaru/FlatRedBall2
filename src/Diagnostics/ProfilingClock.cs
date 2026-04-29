using System.Diagnostics;

namespace FlatRedBall2.Diagnostics;

// High-resolution timestamp helper used by Screen.Update / FlatRedBallService.Update / Draw to
// fill in FrameProfile fields. Stopwatch.GetTimestamp is RDTSC-backed on x64 (~5-10ns per call),
// cheap enough to leave on per-frame in release.
internal static class ProfilingClock
{
    private static readonly double TickToMs = 1000.0 / Stopwatch.Frequency;

    // Wall-clock milliseconds between two Stopwatch.GetTimestamp() values.
    public static double Ms(long startTicks, long endTicks) => (endTicks - startTicks) * TickToMs;
}
