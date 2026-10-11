using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Export.Gif;

/// <summary>
/// GIF stores each frame's delay in whole centiseconds, and browsers play anything under 2cs
/// (20ms) at 10cs, so 50fps is the fastest rate that survives. This plans the sampling grid
/// and per-frame delays for a chosen rate.
/// </summary>
public static class GifTiming
{
    public const int MinDelayCentiseconds = 2;
    public const int MaxFrameRate = 50;

    /// <summary>The rates the export dialog offers, fastest first.</summary>
    public static IReadOnlyList<int> SupportedFrameRates { get; } = new[] { 50, 30, 25, 10 };

    /// <summary>
    /// One delay (centiseconds) per sampled step; step <c>k</c> is sampled at <c>k / frameRate</c>
    /// seconds. Rates that are not a whole number of centiseconds (30fps is 3.33cs) alternate
    /// delays so the running total stays exact. A tail step shorter than the browser minimum is
    /// folded into the step before it.
    /// </summary>
    public static int[] PlanDelays(double durationSeconds, int frameRate)
    {
        if (frameRate < 1 || frameRate > MaxFrameRate)
            throw new ArgumentOutOfRangeException(nameof(frameRate), $"GIF supports 1 to {MaxFrameRate} fps.");

        int endCs = Math.Max(MinDelayCentiseconds, Round(durationSeconds * 100));
        int steps = Math.Max(1, (int)Math.Ceiling(durationSeconds * frameRate - 1e-6));

        while (steps > 1 && endCs - Start(steps - 1, frameRate) < MinDelayCentiseconds)
            steps--;

        var delays = new int[steps];
        for (int k = 0; k < steps; k++)
        {
            int next = k == steps - 1 ? endCs : Start(k + 1, frameRate);
            delays[k] = next - Start(k, frameRate);
        }
        return delays;
    }

    private static int Start(int step, int frameRate) => Round(step * 100.0 / frameRate);

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
