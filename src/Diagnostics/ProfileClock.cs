using System.Diagnostics;

namespace FlatRedBall2.Diagnostics;

// High-resolution timestamp helper used by Screen.Update / FlatRedBallService.Update / Draw to
// fill in FrameProfile fields. RDTSC-backed on desktop x64 (~5-10ns per call), cheap enough to
// leave on per-frame in release.
//
// Under WebAssembly there is no hardware-timer path: every clock routes through the browser's
// performance.now(), which is deliberately coarsened as a Spectre mitigation (roughly 1ms on
// Firefox and Safari, 0.1ms on Chrome, finer only in a cross-origin-isolated page). Stopwatch
// .Frequency still reports a nominal frequency that does NOT reflect that clamp, so
// MeasureResolutionMs probes the real step rather than trusting it.
internal static class ProfileClock
{
    private static readonly double TickToMs = 1000.0 / Stopwatch.Frequency;

    // Each sample blocks until the clock ticks, so this directly bounds the probe's cost:
    // ~10ms on a 1ms clock, microseconds on desktop. The minimum of 10 samples is plenty.
    private const int ResolutionSampleCount = 10;

    // Guards against a clock that never advances — without it the spin below would hang.
    private const int MaxSpinsPerSample = 10_000_000;

    // Wall-clock milliseconds between two Stopwatch.GetTimestamp() values.
    public static double Ms(long startTicks, long endTicks) => (endTicks - startTicks) * TickToMs;

    // Smallest observable step of the underlying clock, in milliseconds — the granularity every
    // FrameProfile measurement is quantized to. Spins until the timestamp changes and takes the
    // smallest jump seen. Returns 0 if the clock never advanced.
    public static double MeasureResolutionMs()
    {
        long smallestTickJump = long.MaxValue;

        for (int sample = 0; sample < ResolutionSampleCount; sample++)
        {
            long start = Stopwatch.GetTimestamp();
            long next = start;
            for (int spin = 0; spin < MaxSpinsPerSample && next == start; spin++)
                next = Stopwatch.GetTimestamp();

            if (next == start) return 0;
            if (next - start < smallestTickJump) smallestTickJump = next - start;
        }

        return smallestTickJump * TickToMs;
    }
}
