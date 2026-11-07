using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Remembers the timeline zoom of each <c>.seqj</c> in the editor settings, as the whole percent the toolbar shows.
/// A percent survives a change of the pixels-per-second constant that a pixel value would not.
/// </summary>
public static class TimelineZoomMemory
{
    public const int MinPercent = 0;
    public const int MaxPercent = 0;

    /// <summary>Records <paramref name="zoomPercent"/> under <paramref name="fileKey"/>. Returns true when <paramref name="store"/> changed.</summary>
    public static bool Remember(Dictionary<string, int> store, string fileKey, double zoomPercent) => false;

    /// <summary>The zoom percent to apply when <paramref name="fileKey"/> opens: its remembered one within the timeline limits, else 100.</summary>
    public static int PercentToRestore(Dictionary<string, int> store, string fileKey) => 100;
}
