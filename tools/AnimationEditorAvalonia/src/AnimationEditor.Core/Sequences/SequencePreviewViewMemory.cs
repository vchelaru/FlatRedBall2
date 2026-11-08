using AnimationEditor.Core.Rendering;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>The sequence preview's view of one <c>.seqj</c>: zoom as the whole percent its toolbar shows, and the pan in pixels.</summary>
public sealed class SequencePreviewView
{
    public int ZoomPercent { get; set; } = 100;
    public float PanX { get; set; }
    public float PanY { get; set; }
}

/// <summary>
/// Remembers the preview zoom and pan of each <c>.seqj</c> in the editor settings, the way an <c>.achx</c> keeps its
/// <c>PreviewZoomPercent</c> and <c>PreviewPan</c> in its companion file. A file at the default view has no entry.
/// </summary>
public static class SequencePreviewViewMemory
{
    private const int DefaultPercent = 100;

    private static readonly int MinPercent = (int)Math.Round(CanvasTransform.MinZoom * 100f);
    private static readonly int MaxPercent = (int)Math.Round(CanvasTransform.MaxZoom * 100f);

    /// <summary>
    /// Records the view under <paramref name="fileKey"/>, zoom rounded and held within the preview limits. The default view
    /// (100%, no pan) is what a file opens at anyway, so it clears the entry. Returns true when <paramref name="store"/> changed.
    /// </summary>
    public static bool Remember(Dictionary<string, SequencePreviewView> store, string fileKey, double zoomPercent, float panX, float panY)
    {
        var view = Sanitize(new SequencePreviewView { ZoomPercent = ClampPercent(zoomPercent), PanX = panX, PanY = panY });
        if (view.ZoomPercent == DefaultPercent && view.PanX == 0 && view.PanY == 0) return store.Remove(fileKey);
        if (store.TryGetValue(fileKey, out var stored) && stored.ZoomPercent == view.ZoomPercent && stored.PanX == view.PanX && stored.PanY == view.PanY)
            return false;
        store[fileKey] = view;
        return true;
    }

    /// <summary>The view to apply when <paramref name="fileKey"/> opens: its remembered one within the preview limits (a damaged value falls back), else the default.</summary>
    public static SequencePreviewView ViewToRestore(Dictionary<string, SequencePreviewView> store, string fileKey) =>
        store.TryGetValue(fileKey, out var stored)
            ? Sanitize(new SequencePreviewView { ZoomPercent = ClampPercent(stored.ZoomPercent), PanX = stored.PanX, PanY = stored.PanY })
            : new SequencePreviewView();

    private static int ClampPercent(double percent) => (int)Math.Clamp(Math.Round(percent), MinPercent, MaxPercent);

    private static SequencePreviewView Sanitize(SequencePreviewView view)
    {
        if (!float.IsFinite(view.PanX)) view.PanX = 0;
        if (!float.IsFinite(view.PanY)) view.PanY = 0;
        return view;
    }
}
