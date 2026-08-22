using AnimationEditor.Core.Diff;
using AnimationEditor.Core.Rendering;
using Avalonia.Input;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A read-only PNG viewer tab (issue #604). Inherits the full pan/zoom camera, canvas-palette
/// background, grid, scrollbar ranges, middle-mouse / Alt-left panning, and wheel zoom-at-cursor
/// from <see cref="TextureViewport"/>.
/// <para>
/// It adds the git-diff <b>region boxes</b> (issue #606) drawn over the image via the base
/// <see cref="TextureViewportSnapshot.DrawOverlay"/> hook, so the boxes pan and zoom with the texture
/// for free. On a revision select (<see cref="SetDiffRegions"/> with <c>frame: true</c>) the camera
/// also frames the changed region(s) into view and plays a one-shot bounce so a small or off-screen
/// change is easy to spot on a large sheet.
/// </para>
/// </summary>
public sealed class PngPreviewControl : TextureViewport
{
    // Changed-region boxes in texture-space pixel coordinates (from RegionMerger). Empty = no overlay.
    private IReadOnlyList<PixelRegion> _diffRegions = Array.Empty<PixelRegion>();

    // Diff-region outline: a warm red distinct from the wireframe's blue frame boxes.
    private static readonly SKColor RegionOutline = new(230, 74, 60, 235);

    // Usage-overlay chain groups (issue #953), texture-space pixel rects per chain. Empty = hidden.
    private IReadOnlyList<UsageChainRegions> _usageGroups = Array.Empty<UsageChainRegions>();

    // Transparent fill + hard outline per rect (design decision on #953): overlapping regions from
    // different chains simply stack their alpha, no special blend handling needed.
    private const byte UsageFillAlpha = 60;
    private const byte UsageStrokeAlpha = 220;

    // Framing: fit the changed region(s) to this fraction of the viewport (leaving surrounding
    // context), and never magnify past this zoom so a 1-pixel change centers instead of exploding.
    private const float FocusFitFraction = 0.6f;
    private const float FocusMaxZoom = 8f;

    // One-shot reveal bounce (#606 / #803). RevealHost owns progress + timer.
    private RevealHost _reveal = null!;

    public PngPreviewControl()
    {
        _reveal = new RevealHost(InvalidateVisual);
        DetachedFromVisualTree += (_, _) => _reveal.Stop();
    }

    /// <summary>
    /// Shows a historical revision's decoded image (#606) so the diff boxes overlay the exact pixels
    /// they were computed against, rather than the current on-disk texture. Call the base
    /// <see cref="TextureViewport.ForceReloadTexture"/> to return to the current file.
    /// </summary>
    public void ShowRevisionImage(ImageData image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        // SKBitmap(info) allocates a tightly-packed RGBA buffer (RowBytes == Width*4), matching
        // ImageData's row-major layout, so the whole buffer copies in one shot.
        var bitmap = new SKBitmap(info);
        Marshal.Copy(image.Rgba, 0, bitmap.GetPixels(), image.Rgba.Length);
        ShowDecodedTexture(bitmap);   // takes ownership of the bitmap
    }

    /// <summary>
    /// Replaces the changed-region overlay with <paramref name="regions"/> (texture-space pixels) and
    /// repaints. When <paramref name="frame"/> is true and there are regions, the camera also frames
    /// them into view and plays the reveal bounce — used on a revision select. Pass <c>false</c> for a
    /// same-revision update (e.g. a merge-distance slider drag) so the view doesn't jump. An empty list
    /// clears the overlay.
    /// </summary>
    public void SetDiffRegions(IReadOnlyList<PixelRegion> regions, bool frame)
    {
        _diffRegions = regions;

        if (frame && regions.Count > 0)
        {
            FrameRegions();
            _reveal.Restart();
        }

        InvalidateVisual();
    }

    /// <summary>True while the PNG diff-region reveal (#606) is easing toward rest.</summary>
    public bool IsDiffRevealAnimating => _reveal.IsAnimating;

    /// <summary>Test-only: reveal progress (0 = full bump, 1 = settled).</summary>
    public float DiffRevealProgress => _reveal.Progress;

    /// <summary>
    /// Advances the in-flight diff reveal by <paramref name="dtSeconds"/>. Returns <c>true</c>
    /// while still animating. Tests drive this for determinism.
    /// </summary>
    public bool StepDiffReveal(float dtSeconds) => _reveal.Step(dtSeconds);

    /// <summary>Runs <see cref="StepDiffReveal"/> to completion synchronously.</summary>
    public void SettleDiffReveal() => _reveal.Settle();

    /// <summary>
    /// Replaces the "Analyze Image Usage" overlay (issue #953) with <paramref name="groups"/> — one
    /// entry per animation chain that references the currently-displayed PNG, each in its own
    /// color — and repaints. An empty list hides the overlay.
    /// </summary>
    public void SetUsageRegions(IReadOnlyList<UsageChainRegions> groups)
    {
        _usageGroups = groups;
        InvalidateVisual();
    }

    /// <summary>Currently-shown usage-overlay groups. Test-only; production code drives this via
    /// <see cref="SetUsageRegions"/>, not by reading it back.</summary>
    public IReadOnlyList<UsageChainRegions> UsageRegions => _usageGroups;

    /// <summary>
    /// Fired when a left-click lands inside one or more usage-overlay rects. Empty click points (no
    /// overlay shown, or the click missed every rect) never raise this. More than one candidate means
    /// overlapping chains — the caller decides how to disambiguate (e.g. a candidate-picker popup).
    /// </summary>
    public event Action<IReadOnlyList<UsageChainRegions>>? UsageRegionsClicked;

    /// <inheritdoc />
    protected override void OnEditPointerPressed(PointerPressedEventArgs e)
    {
        base.OnEditPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(this);
        HitTestUsageRegions(ScreenToTexture((float)pos.X, (float)pos.Y));
    }

    /// <summary>Test-only: simulates a click at a texture-space point, exercising the same hit test
    /// as a real left-click (<see cref="OnEditPointerPressed"/>) without needing pointer routing.</summary>
    public void SimulateUsageRegionClick(float textureX, float textureY) =>
        HitTestUsageRegions(new SKPoint(textureX, textureY));

    private void HitTestUsageRegions(SKPoint world)
    {
        if (_usageGroups.Count == 0) return;
        var hits = _usageGroups.Where(g => g.Rects.Any(r => r.Contains(world))).ToList();
        if (hits.Count > 0)
            UsageRegionsClicked?.Invoke(hits);
    }

    // Fits the combined bounds of all changed regions into the viewport and centers them.
    private void FrameRegions()
    {
        if (Bounds.Width <= 1 || Bounds.Height <= 1) return;

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var r in _diffRegions)
        {
            if (r.MinX < minX) minX = r.MinX;
            if (r.MinY < minY) minY = r.MinY;
            if (r.MaxX + 1 > maxX) maxX = r.MaxX + 1;   // inclusive bounds → half-open rect
            if (r.MaxY + 1 > maxY) maxY = r.MaxY + 1;
        }

        var (panX, panY, zoom) = CanvasTransform.FitRect(
            minX, minY, maxX, maxY, (float)Bounds.Width, (float)Bounds.Height,
            FocusFitFraction, FocusMaxZoom);

        SetCamera(panX, panY, zoom);
        ClampCamera();
        RaiseViewChanged();
    }

    private sealed class DiffSnapshot : TextureViewportSnapshot
    {
        // Texture-space rects captured on the UI thread for the render thread to draw.
        public List<SKRect> Boxes = new();
        public float RevealProgress = 1f;

        // Usage-overlay groups (#953), captured alongside the diff boxes so one DrawOverlay pass
        // draws both without either control state or a second snapshot type.
        public List<UsageChainRegions> UsageGroups = new();
    }

    /// <inheritdoc />
    protected override TextureViewportSnapshot BuildSnapshot(double width, double height)
    {
        var snap = new DiffSnapshot { DrawOverlay = DrawOverlays, RevealProgress = _reveal.Progress };
        PopulateBaseSnapshot(snap, width, height);
        foreach (var r in _diffRegions)
            // Inclusive pixel bounds → half-open rect: a 1-pixel region spans one pixel of width.
            snap.Boxes.Add(new SKRect(r.MinX, r.MinY, r.MaxX + 1, r.MaxY + 1));
        snap.UsageGroups.AddRange(_usageGroups);
        return snap;
    }

    // Runs on the render thread from immutable snapshot data only — never touches live control state.
    private static void DrawOverlays(SKCanvas canvas, TextureViewportSnapshot snapshot)
    {
        var s = (DiffSnapshot)snapshot;
        DrawDiffOverlay(canvas, s);
        DrawUsageOverlay(canvas, s);
    }

    private static void DrawDiffOverlay(SKCanvas canvas, DiffSnapshot s)
    {
        if (s.Boxes.Count == 0) return;

        float scale = RevealAnimation.Scale(s.RevealProgress);

        using var stroke = new SKPaint
        {
            Color = RegionOutline,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true,
        };
        foreach (var box in s.Boxes)
        {
            var screen = s.TextureRectToScreen(box);
            // Scale in screen space around the box center so the bounce is visually uniform at any zoom.
            if (scale != 1f)
                screen = ScaleAround(screen, scale);
            canvas.DrawRect(screen, stroke);
        }
    }

    // Transparent fill + hard outline per rect, one color per chain (#953 design decision) —
    // overlapping regions from different chains stack their alpha with no special blend logic.
    private static void DrawUsageOverlay(SKCanvas canvas, DiffSnapshot s)
    {
        foreach (var group in s.UsageGroups)
        {
            using var fill = new SKPaint { Color = group.Color.WithAlpha(UsageFillAlpha), Style = SKPaintStyle.Fill };
            using var stroke = new SKPaint
            {
                Color = group.Color.WithAlpha(UsageStrokeAlpha),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true,
            };
            foreach (var rect in group.Rects)
            {
                var screen = s.TextureRectToScreen(rect);
                canvas.DrawRect(screen, fill);
                canvas.DrawRect(screen, stroke);
            }
        }
    }

    private static SKRect ScaleAround(SKRect r, float scale)
    {
        float cx = r.MidX, cy = r.MidY;
        float halfW = r.Width * 0.5f * scale;
        float halfH = r.Height * 0.5f * scale;
        return new SKRect(cx - halfW, cy - halfH, cx + halfW, cy + halfH);
    }
}
