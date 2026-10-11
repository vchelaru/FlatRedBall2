using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Export.Gif;

/// <summary>
/// GIF stores each frame's delay in whole centiseconds, and browsers play anything under 2cs
/// (20ms) at 10cs, so 50fps is the fastest rate that survives.
/// </summary>
public static class GifTiming
{
    public const int TicksPerSecond = 100;
    public const int MinDelayCentiseconds = 2;
    public const int MaxFrameRate = 50;

    /// <summary>The rates the export dialog offers, fastest first.</summary>
    public static IReadOnlyList<int> SupportedFrameRates { get; } = new[] { 50, 30, 25, 10 };

    /// <summary>One delay (centiseconds) per sampled step; see <see cref="ExportTiming.PlanDelays"/>.</summary>
    public static int[] PlanDelays(double durationSeconds, int frameRate)
    {
        if (frameRate < 1 || frameRate > MaxFrameRate)
            throw new ArgumentOutOfRangeException(nameof(frameRate), $"GIF supports 1 to {MaxFrameRate} fps.");

        return ExportTiming.PlanDelays(durationSeconds, frameRate, TicksPerSecond, MinDelayCentiseconds);
    }
}
