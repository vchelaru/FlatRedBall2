using AnimationEditor.Core.Diff;
using AnimationEditor.Core.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A read-only PNG viewer tab (issue #604). Inherits the full pan/zoom camera, canvas-palette
/// background, grid, scrollbar ranges, middle-mouse / Alt-left panning, and wheel zoom-at-cursor
/// from <see cref="TextureViewport"/>.
/// <para>
/// It adds one thing: an optional set of git-diff <b>region boxes</b> (issue #606) drawn over the
/// image via the base <see cref="TextureViewportSnapshot.DrawOverlay"/> hook, so the boxes pan and
/// zoom with the texture for free. The Diff/Blame panel sets them with <see cref="SetDiffRegions"/>.
/// </para>
/// </summary>
public sealed class PngPreviewControl : TextureViewport
{
    // Changed-region boxes in texture-space pixel coordinates (from RegionMerger). Empty = no overlay.
    private IReadOnlyList<PixelRegion> _diffRegions = Array.Empty<PixelRegion>();

    // Diff-region outline: a warm red distinct from the wireframe's blue frame boxes.
    private static readonly SKColor RegionOutline = new(230, 74, 60, 235);

    /// <summary>
    /// Replaces the changed-region overlay with <paramref name="regions"/> (texture-space pixels) and
    /// repaints. Pass an empty list to clear the overlay (e.g. when no revision is selected).
    /// </summary>
    public void SetDiffRegions(IReadOnlyList<PixelRegion> regions)
    {
        _diffRegions = regions;
        InvalidateVisual();
    }

    private sealed class DiffSnapshot : TextureViewportSnapshot
    {
        // Texture-space rects captured on the UI thread for the render thread to draw.
        public List<SKRect> Boxes = new();
    }

    /// <inheritdoc />
    protected override TextureViewportSnapshot BuildSnapshot(double width, double height)
    {
        var snap = new DiffSnapshot { DrawOverlay = DrawDiffOverlay };
        PopulateBaseSnapshot(snap, width, height);
        foreach (var r in _diffRegions)
            // Inclusive pixel bounds → half-open rect: a 1-pixel region spans one pixel of width.
            snap.Boxes.Add(new SKRect(r.MinX, r.MinY, r.MaxX + 1, r.MaxY + 1));
        return snap;
    }

    // Runs on the render thread from immutable snapshot data only — never touches live control state.
    private static void DrawDiffOverlay(SKCanvas canvas, TextureViewportSnapshot snapshot)
    {
        var s = (DiffSnapshot)snapshot;
        if (s.Boxes.Count == 0) return;

        using var stroke = new SKPaint
        {
            Color = RegionOutline,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true,
        };
        foreach (var box in s.Boxes)
        {
            var (l, t, r, b) = CanvasTransform.TextureRectToScreen(
                box.Left, box.Top, box.Right, box.Bottom, s.PanX, s.PanY, s.Zoom);
            canvas.DrawRect(new SKRect(l, t, r, b), stroke);
        }
    }
}
