using System;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>The time ruler along the top of the sequence timeline: where its ticks fall and what they say.</summary>
public static class TimelineRuler
{
    private const double MinTickPixels = 60;

    /// <summary>Seconds between ticks: the smallest step that leaves at least 60 pixels between labels.</summary>
    public static double TickStep(double pixelsPerSecond)
    {
        double[] steps = { 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60 };
        return steps.FirstOrDefault(step => step * pixelsPerSecond >= MinTickPixels, 120);
    }

    /// <summary>A tick's label: seconds with up to two decimals under a 1 s step, whole seconds from there up.</summary>
    public static string FormatTick(double time, double step) =>
        (step < 1 ? time.ToString("0.##", CultureInfo.InvariantCulture) : Math.Round(time).ToString(CultureInfo.InvariantCulture)) + "s";
}
