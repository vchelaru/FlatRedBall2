using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure helper that steps a zoom percentage to the next or previous entry in a
/// preset list. Every zoom surface (wireframe, preview, PNG diff, sequence timeline) steps through
/// <see cref="Presets"/> with this one function, so they all behave the same.
/// </summary>
public static class ZoomPresetStepper
{
    /// <summary>Zoom presets (percent) the +/- buttons and mouse-wheel step through, ascending.</summary>
    public static readonly int[] Presets =
        { 5, 10, 16, 25, 33, 50, 66, 75, 100, 150, 200, 300, 400, 800, 1600, 3200 };

    /// <summary>Tolerance used when snapping <paramref name="currentPct"/> to a preset value.</summary>
    private const float Epsilon = 0.01f;

    /// <summary>
    /// Returns the next preset above (<paramref name="direction"/> &gt; 0) or below
    /// (<paramref name="direction"/> &lt; 0) <paramref name="currentPct"/>.
    /// <para>
    /// Float drift is handled by snapping <paramref name="currentPct"/> to the nearest
    /// preset when they are within <c>0.01</c> of each other, so that zooming in while
    /// standing exactly on a preset always advances to the next one.
    /// </para>
    /// </summary>
    /// <param name="currentPct">Current zoom as a percentage (e.g. 100 for 100 %).</param>
    /// <param name="presets">Sorted ascending array of preset percentages.  Must not be empty.</param>
    /// <param name="direction">+1 for zoom-in (step up), −1 for zoom-out (step down).</param>
    public static int StepToNextPreset(float currentPct, int[] presets, int direction)
    {
        // Snap float drift: if currentPct is within epsilon of a preset, treat it as exact.
        foreach (var p in presets)
        {
            if (MathF.Abs(currentPct - p) <= Epsilon)
            {
                currentPct = p;
                break;
            }
        }

        if (direction > 0)
        {
            for (int i = 0; i < presets.Length; i++)
                if (presets[i] > currentPct) return presets[i];
            return presets[^1];
        }
        else
        {
            for (int i = presets.Length - 1; i >= 0; i--)
                if (presets[i] < currentPct) return presets[i];
            return presets[0];
        }
    }

    /// <summary>
    /// <see cref="StepToNextPreset"/> on a zoom factor (1.0 = 100%), with the result clamped to
    /// [<paramref name="minZoom"/>, <paramref name="maxZoom"/>]. A preset past a limit lands on the limit, so
    /// the limit is the last notch. Shared by <c>ZoomAnimator</c> and anything else that steps a zoom factor.
    /// </summary>
    public static float StepZoom(float zoom, int direction, int[] presets, float minZoom, float maxZoom) =>
        Math.Clamp(StepToNextPreset(zoom * 100f, presets, direction) / 100f, minZoom, maxZoom);
}
