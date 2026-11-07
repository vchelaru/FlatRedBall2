using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Remembers the timeline zoom of each <c>.seqj</c> in the editor settings, as the whole percent the toolbar shows.
/// A percent survives a change of the pixels-per-second constant that a pixel value would not.
/// </summary>
public static class TimelineZoomMemory
{
    /// <summary>The percent at the timeline's lowest zoom (10 px/s), rounded to what the toolbar field shows.</summary>
    public static readonly int MinPercent = (int)Math.Round(TimelineViewport.MinZoom * 100.0);

    /// <summary>The percent at the timeline's highest zoom (2000 px/s), rounded to what the toolbar field shows.</summary>
    public static readonly int MaxPercent = (int)Math.Round(TimelineViewport.MaxZoom * 100.0);

    private const int DefaultPercent = 100;

    /// <summary>
    /// Records <paramref name="zoomPercent"/> under <paramref name="fileKey"/>, rounded and kept within the timeline
    /// limits. 100% is what a file opens at anyway, so it clears the entry. Returns true when <paramref name="store"/> changed.
    /// </summary>
    public static bool Remember(Dictionary<string, int> store, string fileKey, double zoomPercent)
    {
        int percent = Clamp(zoomPercent);
        if (percent == DefaultPercent) return store.Remove(fileKey);
        if (store.TryGetValue(fileKey, out var stored) && stored == percent) return false;
        store[fileKey] = percent;
        return true;
    }

    /// <summary>The zoom percent to apply when <paramref name="fileKey"/> opens: its remembered one within the timeline limits, else 100.</summary>
    public static int PercentToRestore(Dictionary<string, int> store, string fileKey) =>
        store.TryGetValue(fileKey, out var percent) ? Clamp(percent) : DefaultPercent;

    private static int Clamp(double percent) => (int)Math.Clamp(Math.Round(percent), MinPercent, MaxPercent);
}
