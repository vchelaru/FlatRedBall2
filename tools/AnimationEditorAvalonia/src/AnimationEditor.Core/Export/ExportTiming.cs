using System;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Plans the sampling grid and per-frame delays of an animated image export. Formats store delays
/// in whole ticks (GIF: centiseconds, WebP: milliseconds), so a rate that is not a whole number of
/// ticks per frame alternates delays and the running total stays exact.
/// </summary>
public static class ExportTiming
{
    /// <summary>
    /// One delay (in ticks) per sampled step; step <c>k</c> is sampled at <c>k / frameRate</c> seconds.
    /// A tail step shorter than <paramref name="minTicks"/> is folded into the step before it, and the
    /// result always has at least one step of at least <paramref name="minTicks"/>.
    /// </summary>
    public static int[] PlanDelays(double durationSeconds, int frameRate, int ticksPerSecond, int minTicks)
    {
        if (frameRate < 1) throw new ArgumentOutOfRangeException(nameof(frameRate));

        int end = Math.Max(minTicks, Round(durationSeconds * ticksPerSecond));
        int steps = Math.Max(1, (int)Math.Ceiling(durationSeconds * frameRate - 1e-6));

        while (steps > 1 && end - Start(steps - 1) < minTicks)
            steps--;

        var delays = new int[steps];
        for (int k = 0; k < steps; k++)
        {
            int next = k == steps - 1 ? end : Start(k + 1);
            delays[k] = next - Start(k);
        }
        return delays;

        int Start(int step) => Round(step * (double)ticksPerSecond / frameRate);
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
