using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Export.Webp;

/// <summary>
/// Animated WebP stores delays in milliseconds, so 60fps works (alternating 16 and 17ms). The 10ms
/// floor matches what browsers do to GIF delays, so a tail frame can never be played back slowly.
/// </summary>
public static class WebpTiming
{
    public const int TicksPerSecond = 1000;
    public const int MinDelayMilliseconds = 10;
    public const int MaxFrameRate = 60;

    /// <summary>The largest width or height a WebP frame can have.</summary>
    public const int MaxDimension = 16383;

    /// <summary>The rates the export dialog offers, fastest first.</summary>
    public static IReadOnlyList<int> SupportedFrameRates { get; } = new[] { 60, 50, 30, 25, 10 };

    /// <summary>One delay (milliseconds) per sampled step; see <see cref="ExportTiming.PlanDelays"/>.</summary>
    public static int[] PlanDelays(double durationSeconds, int frameRate)
    {
        if (frameRate < 1 || frameRate > MaxFrameRate)
            throw new ArgumentOutOfRangeException(nameof(frameRate), $"WebP export supports 1 to {MaxFrameRate} fps.");

        return ExportTiming.PlanDelays(durationSeconds, frameRate, TicksPerSecond, MinDelayMilliseconds);
    }
}
