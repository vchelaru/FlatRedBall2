using AnimationEditor.App.Theming;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Immutable snapshot of the editing-agnostic viewport state (texture, camera, grid), captured on
/// the UI thread so the render thread can read it safely. Derived viewports add their own overlay
/// data via a subclass and draw it through <see cref="DrawOverlay"/>.
/// </summary>
public class TextureViewportSnapshot
{
    // SKImage (not SKBitmap) — immutable and explicitly safe to read on the
    // Avalonia render thread while the UI thread holds the source bitmap.
    public SKImage? Image;
    public int ImageWidth, ImageHeight;
    public float PanX, PanY, Zoom;
    public bool ShowGrid;
    public int GridSize;
    public double Width, Height;

    /// <summary>
    /// Editing overlay, drawn AFTER the base layers (texture/outline/grid), on the render thread,
    /// from the immutable data captured on this snapshot. Null for a plain viewer.
    /// </summary>
    public Action<SKCanvas, TextureViewportSnapshot>? DrawOverlay;
}

/// <summary>
/// Avalonia + SkiaSharp control that displays a texture under a manual pan/zoom camera, with
/// analytic pan clamping (#422), smooth wheel zoom (#425), middle-mouse / Alt-left panning,
/// an optional grid overlay, canvas-palette theming, and render diagnostics. It owns every
/// editing-agnostic viewport behavior so both the wireframe editor and the read-only PNG viewer
/// share one implementation.
/// <para>
/// Coordinate systems:
///   Texture-space — pixel coords (0,0)→(W,H) inside the loaded bitmap.
///   Screen-space  — pixel coords within the control bounds (origin = top-left of control).
///   Transform: screenX = panX + textureX * zoom
/// </para>
/// <para>
/// Subclasses add editing on top via <see cref="OnTextureLoaded"/>, <see cref="BuildSnapshot"/>,
/// and the <c>OnEditPointer*</c> hooks — the base handles pan/zoom before any of those fire.
/// </para>
/// </summary>
public class TextureViewport : Control, IZoomTarget
{
    // ── Inner types ───────────────────────────────────────────────────────────

    // Skia GPU resource-cache budget. The sheet's GPU texture (a 4096² sheet is ~64 MB) must fit
    // in this budget to stay cached across frames; otherwise Skia evicts and re-uploads it every
    // frame, which is what made zoomed-out drawing crawl (#514). Letting Skia own the cache (rather
    // than hand-holding an SKImage.ToTextureImage) also means Skia re-uploads correctly after a
    // context purge — e.g. when a menu popup opens — so the texture never goes dangling/blank.
    private const long GpuResourceCacheBytes = 512L * 1024 * 1024;

    private sealed class DrawOp : ICustomDrawOperation
    {
        private readonly TextureViewportSnapshot _s;
        private readonly CanvasPalette _palette;
        private readonly RollingAverage? _drawTimes;   // non-null only when diagnostics are on

        public DrawOp(TextureViewportSnapshot s, CanvasPalette palette, RollingAverage? drawTimes)
        {
            _s = s; _palette = palette; _drawTimes = drawTimes;
            Bounds = new Rect(0, 0, s.Width, s.Height);
        }

        public Rect Bounds { get; }
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation? other) => false;
        // _s.Image is the control's shared, cached SKImage — owned by the control, NOT this op —
        // so the op must not dispose it. Its lifetime is managed in LoadTexture via deferred drop.
        public void Dispose() { }

        public void Render(ImmediateDrawingContext ctx)
        {
            var lease = ctx.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
            if (lease is null) return;
            using (lease)
            {
                // Raise the GPU cache budget so Skia retains the sheet's texture across frames
                // instead of re-uploading it (#514). Idempotent — the setter just stores the cap.
                if (lease.GrContext is { } gr && gr.GetResourceCacheLimit() < GpuResourceCacheBytes)
                    gr.SetResourceCacheLimit(GpuResourceCacheBytes);

                if (_drawTimes is not null)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    RenderSk(lease.SkCanvas, _s, _palette);
                    sw.Stop();
                    _drawTimes.Add(sw.Elapsed.TotalMilliseconds);
                    // GrContext is non-null only on the GPU (ANGLE) backend; null = software raster.
                    DrawTimeOverlay.Draw(lease.SkCanvas, _drawTimes.Average,
                        lease.GrContext != null ? "GPU" : "CPU");
                }
                else
                    RenderSk(lease.SkCanvas, _s, _palette);
            }
        }

        // ── Static rendering logic ────────────────────────────────────────────

        internal static void RenderSk(SKCanvas canvas, TextureViewportSnapshot s, CanvasPalette palette)
        {
            canvas.Clear(palette.Background);

            if (s.Image != null)
            {
                var dest = new SKRect(
                    s.PanX, s.PanY,
                    s.PanX + s.ImageWidth * s.Zoom,
                    s.PanY + s.ImageHeight * s.Zoom);

                // Texture image — point sampling when zoomed ≥ 1× for pixel-art fidelity.
                // s.Image is a GPU-resident texture on the accelerated path (see DrawOp.Render),
                // so sampling this 4096² sheet is constant-time instead of re-uploading the visible
                // slice from CPU memory every frame — the #514 zoom-out cost.
                var sampling = s.Zoom >= 1f
                    ? new SKSamplingOptions(SKFilterMode.Nearest)
                    : new SKSamplingOptions(SKFilterMode.Linear);
                canvas.DrawImage(s.Image, dest, sampling);

                // Outline around whole texture
                using var outlinePaint = new SKPaint
                {
                    Color = palette.TextureOutline,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1f
                };
                canvas.DrawRect(dest, outlinePaint);

                // Grid overlay
                if (s.ShowGrid && s.GridSize > 0)
                    DrawGrid(canvas, s, dest, palette);
            }

            // Editing overlay (frames, handles, preview, origin) — drawn on top of the base layers
            // from immutable snapshot data, using the same pan/zoom the base layers used.
            s.DrawOverlay?.Invoke(canvas, s);
        }

        // Every 4th line is drawn as a "major" line (brighter/thicker) so distances are
        // easier to eyeball at a glance, like graph paper (#539).
        private const int MajorGridLineInterval = 4;

        private static void DrawGrid(SKCanvas canvas, TextureViewportSnapshot s, SKRect textureDest, CanvasPalette palette)
        {
            using var minorPaint = new SKPaint
            {
                Color        = palette.GridLineMinor,
                Style        = SKPaintStyle.Stroke,
                StrokeWidth  = 0.5f,
                IsAntialias  = true
            };
            using var majorPaint = new SKPaint
            {
                Color        = palette.GridLineMajor,
                Style        = SKPaintStyle.Stroke,
                StrokeWidth  = 1f,
                IsAntialias  = true
            };
            float step = s.GridSize * s.Zoom;
            if (step < 1f) step = 1f;

            // Full viewport (not just textureDest) so empty canvas around the sheet still shows
            // the grid — lines stay locked to the texture origin (PanX/PanY).
            float viewL = 0f, viewT = 0f, viewR = (float)s.Width, viewB = (float)s.Height;
            float originX = s.PanX;
            float originY = s.PanY;

            // n = how many grid steps this line sits from the texture origin (PanX/PanY). The
            // major/minor pattern must key off n, not off "which line is first visible" — the
            // latter shifts the pattern's phase every time the camera pans (#701).
            (float pos, int n) FirstLine(float origin, float viewMin)
            {
                int n = (int)MathF.Ceiling((viewMin - origin) / step);
                return (origin + n * step, n);
            }

            // C#'s % can return negative for negative n; fold it into [0, interval).
            bool IsMajor(int n) => ((n % MajorGridLineInterval) + MajorGridLineInterval) % MajorGridLineInterval == 0;

            var (xStart, xIndex) = FirstLine(originX, viewL);
            for (float x = xStart; x <= viewR; x += step, xIndex++)
                canvas.DrawLine(x, viewT, x, viewB, IsMajor(xIndex) ? majorPaint : minorPaint);

            var (yStart, yIndex) = FirstLine(originY, viewT);
            for (float y = yStart; y <= viewB; y += step, yIndex++)
                canvas.DrawLine(viewL, y, viewR, y, IsMajor(yIndex) ? majorPaint : minorPaint);

            // Keep textureDest referenced so callers/tests that pass it stay valid; the full-
            // viewport pass supersedes the old texture-only clip.
            _ = textureDest;
        }
    }

    // ── Fields ────────────────────────────────────────────────────────────────

    protected SKBitmap? _bitmap;
    // False when _bitmap came from LoadTexture's knownBitmap parameter (caller-owned, e.g.
    // ThumbnailService's cache in the browser build) -- guards the Dispose call below so this
    // control never frees a bitmap it doesn't own.
    private bool _ownsBitmap = true;
    // Immutable GPU-uploadable copy of _bitmap, built on the UI thread and
    // safe to draw from the Avalonia render thread.
    private SKImage? _image;
    protected string? _loadedTexturePath;

    // Bumped by every load/swap so an in-flight LoadTextureAsync decode that finishes after a newer
    // load can detect it was superseded and discard its result instead of painting stale pixels.
    private int _textureLoadId;

    protected float _zoom = 1f;
    // Camera pan: the screen position (within the control's viewport) of texture pixel (0,0).
    // screenX = panX + textureX * zoom. Clamped analytically by ClampCamera — there is no
    // ScrollViewer; the control IS the viewport and two ScrollBars are driven from this pan.
    protected float _panX, _panY;

    // ── Smooth (animated) wheel zoom (#425) ───────────────────────────────────
    // A wheel notch retargets _zoomTarget; the timer eases _zoom toward it via ZoomChase,
    // applying each stepped value through the existing pivot-preserving ZoomToward. The pivot
    // is stored in VIEWPORT space and re-used every tick, so the point under the cursor stays
    // fixed for the whole animation — the per-tick factors compose to the same result as one
    // instant notch. Rapid notches retarget the in-flight animation rather than stacking.
    private DispatcherTimer? _zoomTimer;
    private bool  _zoomAnimating;
    private float _zoomTarget;      // destination zoom factor (1.0 = 100 %)
    private float _zoomPivotVpX;    // cursor pivot, viewport space
    private float _zoomPivotVpY;
    private const float ZoomAnimIntervalSeconds = 1f / 60f;

    protected bool _showGrid;
    protected int _gridSize = 16;

    // Set when LoadTexture/CenterTexture ran before the control had a real viewport
    // (Bounds not yet laid out); the first SizeChanged with valid Bounds re-centers.
    private bool _needsInitialCenter;

    // ── Render diagnostics (#514): one overlay, toggled at runtime by F3 / Help menu ──
    // When enabled the panel shows the rolling-average Skia render time (ms/frame + fps, drawn
    // top-left inside the custom draw op) AND a camera-stats panel stacked below it. This is THE
    // panel to watch for the #514 slowdown — panning/zooming a large sheet redraws it (60fps during
    // smooth-zoom). Toggle via DiagnosticsEnabled; MainWindow wires F3 and the Help menu item.
    private bool _showDiagnostics;
    private readonly RollingAverage _drawTimes = new(10);
    private DispatcherTimer? _diagnosticsTimer;
    private static readonly Typeface _dbgTypeface = new("Consolas, Courier New");
    // ImmutableSolidColorBrush has no thread affinity and is safe to use from the compositor thread.
    private static readonly IImmutableBrush _dbgBg = new ImmutableSolidColorBrush(Color.FromArgb(210, 0, 0, 0));
    private static readonly IImmutableBrush _dbgFg = new ImmutableSolidColorBrush(Color.FromRgb(0, 255, 80));

    // Neutral canvas/grid/outline colors for the active theme variant. Refreshed from
    // ActualThemeVariant on every render and whenever the variant changes.
    private CanvasPalette _palette = CanvasPalette.Dark;

    private uint? _canvasBackgroundOverride;

    private bool _isPanning;
    private Point _panAnchor;
    private float _panAnchorX, _panAnchorY;

    // Per-texture saved camera (texture path → panX, panY, zoom). panX/panY are the screen
    // position of texture pixel (0,0) — the full camera pan in the analytic model.
    private readonly System.Collections.Generic.Dictionary<string, (float px, float py, float z)> _cameraByTexture = new();

    // ── Public properties ─────────────────────────────────────────────────────

    /// <summary>Absolute path of the currently displayed texture, or null.</summary>
    public string? LoadedTexturePath => _loadedTexturePath;

    /// <summary>Pixel dimensions of the loaded bitmap (0×0 when nothing is loaded).</summary>
    public (int Width, int Height) BitmapSize =>
        _bitmap is null ? (0, 0) : (_bitmap.Width, _bitmap.Height);

    /// <summary>Current zoom factor (1.0 = 100 %).</summary>
    public float Zoom => _zoom;

    /// <summary>
    /// Optional user-chosen canvas background as a packed <c>0xAARRGGBB</c> value; <c>null</c>
    /// follows the theme. Setting it re-resolves the palette and repaints. Chrome (grid, rulers,
    /// outline) stays theme-driven.
    /// </summary>
    public uint? CanvasBackgroundOverride
    {
        get => _canvasBackgroundOverride;
        set
        {
            if (_canvasBackgroundOverride == value) return;
            _canvasBackgroundOverride = value;
            UpdatePalette();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Shows/hides the render-diagnostics overlay (draw-time readout + camera stats). Toggled at
    /// runtime from MainWindow (F3 and the Help ▸ Show Render Diagnostics menu item).
    /// </summary>
    public bool DiagnosticsEnabled
    {
        get => _showDiagnostics;
        set
        {
            if (_showDiagnostics == value) return;
            _showDiagnostics = value;
            // The panel only repaints on demand (pan/zoom/selection), so an idle overlay would show
            // a frozen ms/frame. While diagnostics are on, tick a 1 fps repaint so the readout stays
            // live even when nothing else changes; stop it otherwise to keep the panel idle.
            if (value)
            {
                _diagnosticsTimer ??= CreateDiagnosticsTimer();
                _diagnosticsTimer.Start();
            }
            else
                _diagnosticsTimer?.Stop();
            InvalidateVisual();
        }
    }

    private DispatcherTimer CreateDiagnosticsTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => InvalidateVisual();
        return timer;
    }

    // Camera-stats panel. Sits below the ms/frame box (drawn by the custom op via DrawTimeOverlay),
    // so it starts at TopOffset to avoid overlapping it — both are left-aligned under one toggle.
    private void DrawDebugOverlay(DrawingContext ctx)
    {
        if (!_showDiagnostics) return;
        int bmpW = _bitmap?.Width  ?? 0;
        int bmpH = _bitmap?.Height ?? 0;

        var lines = new[]
        {
            "── WIREFRAME (F3 to hide) ──",
            $"zoom          {_zoom * 100f,7:F1}%",
            $"panXY         X={_panX,7:F1}  Y={_panY:F1}",
            $"viewport      {Bounds.Width:F0} × {Bounds.Height:F0}",
            $"content       {bmpW * _zoom:F0} × {bmpH * _zoom:F0}",
            $"isPanning     {_isPanning}",
        };

        const double fsz  = 12;
        const double lineH = 15;
        const double padX  = 6;
        const double padY  = 4;
        const double topOffset = 28;   // clears the ms/frame box the draw op renders at the top
        double panelW = 310;
        double panelH = lines.Length * lineH + padY * 2;

        ctx.FillRectangle(_dbgBg, new Rect(0, topOffset, panelW, panelH));
        double y = topOffset + padY;
        foreach (var line in lines)
        {
            ctx.DrawText(new FormattedText(
                line, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                _dbgTypeface, fsz, _dbgFg), new Point(padX, y));
            y += lineH;
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired after every zoom change. Payload is the new zoom as a percentage (e.g. 100f = 100 %).
    /// </summary>
    public event Action<float>? ZoomChanged;

    /// <summary>Fired when the user finishes a pan gesture (pointer released after drag).</summary>
    public event Action<float, float>? PanChanged;

    /// <summary>
    /// Fired whenever the camera (pan, zoom) or the viewport size changes, so the host can
    /// refresh the two <c>ScrollBar</c>s from <see cref="GetScrollBarRanges"/>. Distinct from
    /// <see cref="PanChanged"/>, which fires only on pan-gesture completion for persistence.
    /// </summary>
    public event Action? ViewChanged;

    protected void RaiseViewChanged() => ViewChanged?.Invoke();

    /// <summary>Raises <see cref="ZoomChanged"/> with the current zoom as a percentage, so a bound
    /// zoom combo re-syncs. Subclasses call this after changing <see cref="_zoom"/> directly.</summary>
    protected void RaiseZoomChanged() => ZoomChanged?.Invoke(_zoom * 100f);

    /// <summary>
    /// When non-null, mouse-wheel zoom steps through these preset percentages instead of
    /// applying a raw ×1.25 / ÷1.25 multiplier.  Set by <c>MainWindow</c> on startup.
    /// <para>
    /// Standalone controls (no <c>MainWindow</c>) leave this null and retain the legacy
    /// multiplier behaviour, which is still useful in precision-zoom tests.
    /// </para>
    /// </summary>
    public int[]? WheelZoomPresets { get; set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public TextureViewport()
    {
        ClipToBounds = true;
        Focusable = true;

        // Repaint when the app theme variant changes so the canvas/grid/outline colors update.
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();

        // A resize changes the viewport, hence the pan clamp and scrollbar range — re-clamp the
        // camera and refresh the host's scrollbars. If centering was deferred because the control
        // had no real viewport yet (Bounds not laid out), do it now.
        SizeChanged += (_, _) =>
        {
            if (_needsInitialCenter && Bounds.Width > 1 && _bitmap != null)
                CenterTexture();
            else
                ClampCamera();
            RaiseViewChanged();
        };

        // Stop the smooth-zoom and diagnostics timers if the control leaves the tree.
        DetachedFromVisualTree += (_, _) => { StopZoomTimer(); _diagnosticsTimer?.Stop(); };
    }

    // ── Camera clamping + scrollbar integration ───────────────────────────────

    /// <summary>
    /// Clamps the camera pan so the texture's far edge can reach the viewport centre but no
    /// further — the texture is never scrolled fully out of view, yet any texture point can be
    /// brought to the centre. Pure analytic clamp (<see cref="CanvasTransform.ClampWireframePan"/>)
    /// — no ScrollViewer extent dependency, which is what makes a symmetric zoom in/out an exact
    /// round-trip (#422). No-op until layout has produced a real viewport (<c>Bounds.Width &gt; 1</c>)
    /// or with no texture.
    /// </summary>
    protected void ClampCamera()
    {
        if (_bitmap == null || Bounds.Width <= 1) return;
        (_panX, _panY) = CanvasTransform.ClampWireframePan(
            _panX, _panY, (float)Bounds.Width, (float)Bounds.Height,
            _bitmap.Width, _bitmap.Height, _zoom);
    }

    /// <summary>
    /// Scrollbar (Minimum, Maximum, Value, ViewportSize) for each axis, derived from the
    /// current pan, viewport, and texture size. <c>MainWindow</c> applies these to the two
    /// <c>ScrollBar</c>s; the value axis is the negation of the pan axis (see
    /// <see cref="PanScrollBar"/>). Returns a degenerate (zero) range before layout has run
    /// or with no texture loaded.
    /// </summary>
    public (ScrollBarRange Horizontal, ScrollBarRange Vertical) GetScrollBarRanges()
    {
        float viewW = (float)Bounds.Width;
        float viewH = (float)Bounds.Height;
        if (_bitmap == null || viewW <= 1 || viewH <= 1)
            return (new ScrollBarRange(0f, 0f, 0f, 1f), new ScrollBarRange(0f, 0f, 0f, 1f));

        // Centre-relative pan: pan_c = panX − viewW/2; content extent (origin = texture
        // top-left) is [0, bitmap × zoom]; padding −viewport/2 matches ClampWireframePan's band.
        return (
            PanScrollBar.FromPan(_panX - viewW / 2f, viewW, 0f, _bitmap.Width  * _zoom, -viewW / 2f),
            PanScrollBar.FromPan(_panY - viewH / 2f, viewH, 0f, _bitmap.Height * _zoom, -viewH / 2f));
    }

    /// <summary>Sets the horizontal pan from a scrollbar value (see <see cref="PanScrollBar"/>)
    /// and repaints. Clamped defensively to the pan band.</summary>
    public void SetPanX(float scrollValue)
    {
        _panX = PanScrollBar.PanFromValue(scrollValue) + (float)Bounds.Width / 2f;
        ClampCamera();
        InvalidateVisual();
        RaiseViewChanged();
    }

    /// <summary>Sets the vertical pan from a scrollbar value (see <see cref="PanScrollBar"/>)
    /// and repaints. Clamped defensively to the pan band.</summary>
    public void SetPanY(float scrollValue)
    {
        _panY = PanScrollBar.PanFromValue(scrollValue) + (float)Bounds.Height / 2f;
        ClampCamera();
        InvalidateVisual();
        RaiseViewChanged();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads <paramref name="filePath"/> as the displayed texture. Returns <c>true</c> when the
    /// texture is shown (or when <paramref name="filePath"/> is empty, an intentional clear);
    /// returns <c>false</c> when a non-empty path could not be displayed — the file is missing,
    /// or it exists but cannot be decoded as an image (corrupt/truncated/mislabeled/locked).
    /// On failure the control is left in a coherent unloaded state. Callers that persist the
    /// texture name should commit it only when this returns <c>true</c>, so a file the editor
    /// can't display never gets saved into the .achx (issue #479).
    /// <para>
    /// Saves the camera position for the old texture and restores it for the new one. Fires
    /// <see cref="OnTextureLoaded"/> at the end of every path so subclasses can rebuild their
    /// editing state.
    /// </para>
    /// <para>
    /// <paramref name="knownBitmap"/>, when supplied, is used directly instead of reading
    /// <paramref name="filePath"/> from disk — the browser-wasm build has no filesystem, but
    /// already has every dropped/picked texture decoded via ThumbnailService (mirrors
    /// ProjectManager.LoadAnimationChain's knownTextureSizes fix for the same constraint, #535).
    /// <paramref name="filePath"/> is still used as the logical identity (camera-per-texture
    /// cache key, <see cref="LoadedTexturePath"/>) even though nothing is read from it. The
    /// bitmap is treated as caller-owned (e.g. ThumbnailService's cache) and is never disposed
    /// by this control, unlike a bitmap this method decodes itself from disk.
    /// </para>
    /// </summary>
    public bool LoadTexture(string? filePath, SKBitmap? knownBitmap = null)
    {
        // Lowercased + slash-normalized form used only for cache-key comparison and the
        // _loadedTexturePath identity that downstream filter code keys on. The case-preserving
        // form is what actually goes to the filesystem (Linux is case-sensitive).
        string? norm = string.IsNullOrEmpty(filePath) ? null : new FilePath(filePath).Standardized;
        string? casePreserved = string.IsNullOrEmpty(filePath) ? null : new FilePath(filePath).StandardizedCaseSensitive;

        // Any load supersedes an in-flight async decode (LoadTextureAsync) so it can't paint late.
        _textureLoadId++;

        if (_loadedTexturePath == norm)
        {
            // Texture hasn't changed, but the selected frame may have, so update frame rects.
            OnTextureLoaded(_bitmap);
            return true;
        }

        BeginTextureSwap(norm);

        if (knownBitmap != null)
        {
            _bitmap = knownBitmap;
            _ownsBitmap = false;
            _image = SKImage.FromBitmap(_bitmap);

            if (norm != null && _cameraByTexture.TryGetValue(norm, out var knownCam))
            {
                (_panX, _panY, _zoom) = (knownCam.px, knownCam.py, knownCam.z);
                ClampCamera();
                RaiseViewChanged();
                InvalidateVisual();
            }
            else
            {
                CenterTexture();
            }

            OnTextureLoaded(_bitmap);
            return true;
        }

        if (casePreserved != null && File.Exists(casePreserved))
            return InstallDecodedTexture(SKBitmap.Decode(casePreserved), norm!);

        // casePreserved == null means filePath was empty: an intentional clear (success).
        // A non-empty path that isn't on disk is a load failure.
        OnTextureLoaded(_bitmap);
        return casePreserved == null;
    }

    /// <summary>
    /// Like <see cref="LoadTexture(string?)"/>, but decodes the file off the UI thread so opening a
    /// large image doesn't block. The view blanks immediately (fires <see cref="OnTextureLoaded"/>
    /// with null so a subclass can show a loading state); the decoded image appears when ready. A
    /// newer <see cref="LoadTexture(string?)"/> / <see cref="LoadTextureAsync"/> / <see cref="ShowDecodedTexture"/>
    /// supersedes an in-flight decode so it never paints late. Returns true when the image is shown,
    /// false on decode failure, supersession, or a clear-to-missing.
    /// </summary>
    public async Task<bool> LoadTextureAsync(string? filePath)
    {
        string? norm = string.IsNullOrEmpty(filePath) ? null : new FilePath(filePath).Standardized;
        string? casePreserved = string.IsNullOrEmpty(filePath) ? null : new FilePath(filePath).StandardizedCaseSensitive;

        int loadId = ++_textureLoadId;

        if (_loadedTexturePath == norm && _bitmap != null)
        {
            OnTextureLoaded(_bitmap);
            return true;
        }

        BeginTextureSwap(norm);
        OnTextureLoaded(null);   // blank now; a subclass can show "Loading…"
        InvalidateVisual();

        if (casePreserved == null || !File.Exists(casePreserved))
            return casePreserved == null;   // empty path = intentional clear (success); missing = failure

        // Read + decode off the UI thread. Byte-based decode (vs SKBitmap.Decode(path)) is what the
        // rest of the app decodes with, and the file read for a large sheet is itself worth moving off
        // the UI thread.
        var decoded = await Task.Run(() => DecodeFileBytes(casePreserved));

        if (loadId != _textureLoadId)
        {
            // Superseded by a newer load while we were decoding — drop this now-unwanted result.
            decoded?.Dispose();
            return false;
        }

        return InstallDecodedTexture(decoded, norm!);
    }

    private static SKBitmap? DecodeFileBytes(string path)
    {
        try { return SKBitmap.Decode(File.ReadAllBytes(path)); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    // Saves the leaving texture's camera, sets the new identity, and blanks the current image so the
    // view is empty until the (possibly async) decode installs the new one.
    private void BeginTextureSwap(string? norm)
    {
        if (_loadedTexturePath != null)
            _cameraByTexture[_loadedTexturePath] = (_panX, _panY, _zoom);

        _loadedTexturePath = norm;
        // Drop (don't Dispose) the previous image: a render op on the compositor thread may still be
        // drawing it (BuildSnapshot shares _image directly). Releasing the reference lets GC reclaim
        // it once no in-flight draw holds it — deferred drop, mirroring ThumbnailService (#514).
        // The bitmap, by contrast, is never handed to a render op (the image carries its own pixel
        // copy from FromBitmap), so disposing it here stays safe -- unless it's caller-owned.
        _image = null;
        if (_ownsBitmap) _bitmap?.Dispose();
        _bitmap = null;
        _ownsBitmap = true;
    }

    // Installs an already-decoded bitmap as the current texture and restores/centers the camera for
    // `norm`. Assumes BeginTextureSwap already set the identity and blanked the old image. Returns
    // false (clearing identity) when `decoded` is null — a decode failure (#479).
    private bool InstallDecodedTexture(SKBitmap? decoded, string norm)
    {
        if (decoded == null)
        {
            // SKBitmap.Decode returns null (it does NOT throw) when the file exists but can't be
            // decoded — corrupt/truncated/mislabeled PNG, zero-byte file, or a locked file. Handing
            // null to SKImage.FromBitmap would crash the dispatcher (#479); stay unloaded instead.
            _loadedTexturePath = null;
            OnTextureLoaded(_bitmap);
            return false;
        }

        _bitmap = decoded;
        // Upload pixels into an immutable SKImage on the UI thread so the render thread never touches
        // the SKBitmap directly (SKCanvas.DrawBitmap on the render thread would AV).
        _image = SKImage.FromBitmap(decoded);

        if (_cameraByTexture.TryGetValue(norm, out var cam))
        {
            // Restore the full camera and re-clamp against the current viewport (window may have been
            // resized since this texture was last shown).
            (_panX, _panY, _zoom) = (cam.px, cam.py, cam.z);
            ClampCamera();
            RaiseViewChanged();
            InvalidateVisual();
        }
        else
        {
            CenterTexture();
        }

        OnTextureLoaded(_bitmap);
        return true;
    }

    /// <summary>
    /// Replaces the displayed texture with an already-decoded in-memory <paramref name="bitmap"/>,
    /// taking ownership of it. Unlike <see cref="LoadTexture(string?)"/> this leaves the file-path
    /// identity and the saved-camera cache untouched and does not move the camera — used to show a
    /// historical git revision of the current PNG (#606) while the caller frames the change itself.
    /// Call <see cref="ForceReloadTexture"/> to return to the current on-disk file.
    /// </summary>
    public void ShowDecodedTexture(SKBitmap bitmap)
    {
        _textureLoadId++;   // supersede any in-flight async decode so it can't overwrite this
        // Deferred drop of the old image (a compositor draw may still hold it); the old bitmap is
        // never handed to a render op, so disposing it here is safe — mirrors LoadTexture.
        _image = null;
        _bitmap?.Dispose();
        _bitmap = bitmap;
        _image = SKImage.FromBitmap(bitmap);
        OnTextureLoaded(_bitmap);
        InvalidateVisual();
        RaiseViewChanged();
    }

    /// <summary>
    /// Called at the end of every <see cref="LoadTexture"/> path (success, unchanged, clear, or
    /// decode failure) with the newly-loaded bitmap (null when the view is cleared or the decode
    /// failed). The base implementation does nothing; editing subclasses rebuild their per-texture
    /// state here (frame rects, magic-wand image) and repaint.
    /// </summary>
    protected virtual void OnTextureLoaded(SKBitmap? bitmap) { }

    /// <summary>
    /// Force-reload the currently displayed texture from disk, bypassing the identity check.
    /// Use for PNG hot-reload when the file content changed but the path did not.
    /// Must be called on the UI thread.
    /// </summary>
    public void ForceReloadTexture()
    {
        var path = _loadedTexturePath;
        if (path == null) return;
        // LoadTexture only restores a saved camera when _cameraByTexture has an entry for this
        // path, which is populated on cross-texture switches, not on the very first load of a
        // texture. Without saving/restoring here, a hot-reload of the only texture ever opened
        // would find no entry and fall through to CenterTexture(), resetting pan/zoom (#584).
        var savedCamera = (_panX, _panY, _zoom);
        _loadedTexturePath = null;   // clear identity so LoadTexture doesn't short-circuit
        LoadTexture(new FilePath(path).StandardizedCaseSensitive);
        (_panX, _panY, _zoom) = savedCamera;
        ClampCamera();
        RaiseViewChanged();
        InvalidateVisual();
    }

    /// <summary>Set zoom by whole-number percentage (e.g. 100 = 1× fit). Zooms toward the
    /// centre of the viewport.</summary>
    public void SetZoomPercent(int percent)
    {
        CancelZoomAnimation();   // an explicit zoom overrides any in-flight wheel ease
        float newZoom = Math.Clamp(percent / 100f, CanvasTransform.MinZoom, CanvasTransform.MaxZoom);
        ZoomToward((float)Bounds.Width / 2f, (float)Bounds.Height / 2f, newZoom / _zoom);
    }

    /// <summary>Toggle the grid overlay and update the grid cell size.</summary>
    public void SetGrid(bool show, int cellSize)
    {
        _showGrid = show;
        _gridSize = cellSize;
        InvalidateVisual();
    }

    /// <summary>
    /// Directly sets the camera state (pan and zoom) exactly, without clamping — for tests that
    /// need a predictable, axis-aligned view and for restoring a persisted camera. A persisted
    /// camera that lands out of band is re-clamped on the next layout pass (SizeChanged).
    /// </summary>
    public void SetCamera(float panX, float panY, float zoom)
    {
        CancelZoomAnimation();
        _panX = panX;
        _panY = panY;
        _zoom = zoom;
        InvalidateVisual();
        RaiseViewChanged();
    }

    /// <summary>Current grid show/size state. For tests.</summary>
    public (bool ShowGrid, int GridSize) GridState => (_showGrid, _gridSize);

    /// <summary>Camera state (panX, panY, zoom). panX/panY are the screen position of texture
    /// pixel (0,0): screenX = panX + textureX × zoom.</summary>
    public (float PanX, float PanY, float Zoom) CameraState => (_panX, _panY, _zoom);

    // ── Viewport test API ─────────────────────────────────────────────────────

    /// <summary>
    /// Test-only: starts a middle-mouse pan gesture with the anchor at the given
    /// <b>viewport-space</b> position. Call <see cref="SimulatePanMove"/> one or more
    /// times to continue the drag, then <see cref="SimulatePanEnd"/> when done.
    /// No-op when no bitmap is loaded.
    /// </summary>
    public void SimulatePanStart(float vpX, float vpY)
    {
        if (_bitmap is null) return;
        StartPan(new Point(vpX, vpY));
    }

    /// <summary>
    /// Test-only: continues an active pan gesture by supplying the current
    /// <b>viewport-space</b> mouse position. Drives the same pan-delta code path
    /// as <see cref="OnPointerMoved"/>. No-op when no pan is in progress.
    /// </summary>
    public void SimulatePanMove(float vpX, float vpY)
    {
        if (!_isPanning || _bitmap is null) return;
        UpdatePan(vpX, vpY);
    }

    /// <summary>Test-only: ends the pan gesture started by <see cref="SimulatePanStart"/>.</summary>
    public void SimulatePanEnd() => _isPanning = false;

    /// <summary>
    /// Test-only: simulates a single mouse-wheel zoom event toward the given
    /// <b>viewport-space</b> point. Mirrors <see cref="OnPointerWheelChanged"/>.
    /// <para><paramref name="factor"/> is the zoom scale factor (e.g. 1.25 to zoom in by one
    /// wheel notch, 1/1.25 to zoom out). This overload always applies the raw factor regardless
    /// of <see cref="WheelZoomPresets"/> — use it in tests that need deterministic pivot math.</para>
    /// </summary>
    public void SimulateWheelZoom(float vpX, float vpY, float factor) => ZoomToward(vpX, vpY, factor);

    /// <summary>
    /// Test-only: simulates one mouse-wheel notch toward the given <b>viewport-space</b> point
    /// using preset stepping (<see cref="WheelZoomPresets"/>) and runs the resulting smooth-zoom
    /// animation to completion synchronously, so the camera lands on its settled state. Use
    /// <see cref="SimulateWheelZoomBegin"/> instead to observe the animation mid-flight.
    /// </summary>
    public void SimulateWheelZoom(float vpX, float vpY, bool zoomIn)
    {
        BeginAnimatedZoom(vpX, vpY, zoomIn);
        SettleZoomAnimation();
    }

    /// <summary>
    /// Test-only: begins a smooth wheel-zoom toward the <b>viewport-space</b> pivot WITHOUT
    /// settling, so a test can drive <see cref="StepZoomAnimation"/> tick-by-tick and observe the
    /// ease and retargeting. Mirrors the live <see cref="OnPointerWheelChanged"/> path.
    /// </summary>
    public void SimulateWheelZoomBegin(float vpX, float vpY, bool zoomIn) =>
        BeginAnimatedZoom(vpX, vpY, zoomIn);

    /// <summary>True while a smooth wheel-zoom (#425) is easing toward its target. The host gates
    /// per-frame companion-file persistence on this so only the settled state is saved, not every
    /// intermediate tick.</summary>
    public bool IsZoomAnimating => _zoomAnimating;

    /// <summary>Test-only: the zoom factor the in-flight animation is easing toward (1.0 = 100 %).</summary>
    public float TargetZoom => _zoomTarget;

    /// <summary>
    /// Advances the in-flight smooth zoom by <paramref name="dtSeconds"/>, easing toward the
    /// target via <see cref="ZoomChase"/> and applying each step through the pivot-preserving
    /// <see cref="ZoomToward"/>. Returns <c>true</c> while still animating, <c>false</c> once
    /// settled (at which point the timer is stopped). The live 60 fps timer calls this; tests
    /// call it directly for deterministic stepping.
    /// </summary>
    public bool StepZoomAnimation(float dtSeconds)
    {
        if (!_zoomAnimating) return false;

        float next = ZoomChase.Step(_zoom, _zoomTarget, dtSeconds);
        bool settling = ZoomChase.IsSettled(next, _zoomTarget);

        // Clear the flag BEFORE ZoomToward fires ZoomChanged on the settling tick, so the host
        // sees IsZoomAnimating == false and persists the companion file exactly once (on settle).
        if (settling) { _zoomAnimating = false; StopZoomTimer(); }

        // factor is relative to the current zoom; the viewport pivot is constant across ticks, so
        // the factors compose to the same result as a single notch (the pivot stays anchored).
        ZoomToward(_zoomPivotVpX, _zoomPivotVpY, next / _zoom);
        return !settling;
    }

    /// <summary>Runs <see cref="StepZoomAnimation"/> to completion synchronously. Used by the
    /// instant test overloads and available to any caller that must force the settled state.</summary>
    public void SettleZoomAnimation()
    {
        // The 1000-iteration cap is a non-convergence backstop; ZoomChase settles far sooner.
        for (int i = 0; _zoomAnimating && i < 1000; i++)
            StepZoomAnimation(ZoomAnimIntervalSeconds);
    }

    /// <summary>Test-only: current camera pan (screen position of texture pixel (0,0)).</summary>
    public (float X, float Y) PanOffset => (_panX, _panY);

    // ── Rendering ─────────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        UpdatePalette();
        var snap = BuildSnapshot(Bounds.Width, Bounds.Height);
        ctx.Custom(new DrawOp(snap, _palette, _showDiagnostics ? _drawTimes : null));
        DrawDebugOverlay(ctx);
    }

    // ActualThemeVariant resolves Default to the concrete platform variant, so a simple
    // "is it Light?" check correctly handles the follow-system case.
    private void UpdatePalette() =>
        _palette = CanvasPalette.Resolve(ActualThemeVariant != ThemeVariant.Light, _canvasBackgroundOverride);

    /// <summary>
    /// Renders the current viewport state to an off-screen bitmap of the given size.
    /// The current camera (pan/zoom) is used exactly as-is, so call
    /// <see cref="LoadTexture"/> and optionally <see cref="CenterFitForSize"/> first.
    /// <para>
    /// Must be called on the UI thread (same thread that owns <see cref="LoadTexture"/>).
    /// Caller is responsible for disposing the returned bitmap.
    /// </para>
    /// </summary>
    public SKBitmap RenderToBitmap(int width, int height)
    {
        UpdatePalette();
        var snap   = BuildSnapshot(width, height);
        // snap.Image is the shared, control-owned _image (not a per-call clone) — must NOT be
        // disposed here, or the next live render would draw a disposed image (#514).
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        DrawOp.RenderSk(canvas, snap, _palette);
        return bitmap;
    }

    /// <summary>
    /// Sets the camera so the loaded texture is centered and 85 %-fitted inside
    /// a virtual viewport of <paramref name="width"/> × <paramref name="height"/> pixels.
    /// Use this before <see cref="RenderToBitmap"/> in tests that need a predictable view.
    /// </summary>
    public void CenterFitForSize(int width, int height)
    {
        if (_bitmap is null) return;
        CancelZoomAnimation();
        (_panX, _panY, _zoom) = CanvasTransform.CenterFit(
            _bitmap.Width, _bitmap.Height, width, height);
        InvalidateVisual();
    }

    /// <summary>
    /// Populates the editing-agnostic fields (texture, camera, grid, size) of
    /// <paramref name="snap"/>. Subclasses call this from their <see cref="BuildSnapshot"/>
    /// override, then add their own overlay data.
    /// </summary>
    protected void PopulateBaseSnapshot(TextureViewportSnapshot snap, double width, double height)
    {
        // Reuse the immutable image built once per load (issue #514). SKImage is safe to read on
        // the render thread, so a single instance serves every frame — NO per-op atlas copy.
        // 1665108 regressed this to a full bitmap.Copy() per op (67 MB/render on a 4096² sheet);
        // LoadTexture now drops (never synchronously disposes) the old image, so sharing it here
        // can't race a render op mid-draw.
        snap.Image       = _image;
        snap.ImageWidth  = _bitmap?.Width ?? 0;
        snap.ImageHeight = _bitmap?.Height ?? 0;
        snap.PanX        = _panX;
        snap.PanY        = _panY;
        snap.Zoom        = _zoom;
        snap.ShowGrid    = _showGrid;
        snap.GridSize    = _gridSize;
        snap.Width       = width;
        snap.Height      = height;
    }

    /// <summary>
    /// Builds the immutable snapshot the render thread draws from. The base builds only the
    /// editing-agnostic layers (texture/outline/grid); editing subclasses override to return a
    /// snapshot subclass with an overlay (frames, handles, preview).
    /// </summary>
    protected virtual TextureViewportSnapshot BuildSnapshot(double width, double height)
    {
        var snap = new TextureViewportSnapshot();
        PopulateBaseSnapshot(snap, width, height);
        return snap;
    }

    // ── Mouse input ───────────────────────────────────────────────────────────

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // The control IS the viewport now (no ScrollViewer), so e.GetPosition(this) is the
        // viewport-space pivot. Smooth-zoom retargets and eases toward the next preset (#425).
        var pivot = e.GetPosition(this);
        BeginAnimatedZoom((float)pivot.X, (float)pivot.Y, e.Delta.Y > 0);
        StartZoomTimer();   // live driver; tests drive StepZoomAnimation directly instead
        e.Handled = true;
    }

    // ── Smooth (animated) wheel zoom (#425) ───────────────────────────────────

    /// <summary>
    /// Retargets the smooth zoom toward the next/previous preset from the given viewport-space
    /// pivot. A notch while already animating steps from the in-flight <see cref="_zoomTarget"/>,
    /// so rapid spins accumulate through the presets rather than re-targeting the same one from the
    /// mid-animation zoom. Does NOT start the driving timer — the live wheel handler starts it;
    /// tests drive <see cref="StepZoomAnimation"/> directly for determinism.
    /// </summary>
    private void BeginAnimatedZoom(float pivotVpX, float pivotVpY, bool zoomIn)
    {
        float basis = _zoomAnimating ? _zoomTarget : _zoom;
        _zoomTarget   = ComputeTargetZoom(basis, zoomIn);
        _zoomPivotVpX = pivotVpX;
        _zoomPivotVpY = pivotVpY;
        _zoomAnimating = true;
    }

    /// <summary>Stops any in-flight wheel-zoom animation, holding the camera at its current value.
    /// Competing camera actions (pan, combo zoom, centre-on-frame, load) call this so they don't
    /// fight the easing timer.</summary>
    protected void CancelZoomAnimation()
    {
        if (!_zoomAnimating) return;
        _zoomAnimating = false;
        StopZoomTimer();
    }

    /// <summary>The zoom factor one wheel notch targets from <paramref name="basisZoom"/>, using
    /// preset stepping when <see cref="WheelZoomPresets"/> is set, else a 1.25× multiplier. Clamped
    /// to [<see cref="CanvasTransform.MinZoom"/>, <see cref="CanvasTransform.MaxZoom"/>].</summary>
    private float ComputeTargetZoom(float basisZoom, bool zoomIn)
    {
        float targetPct = WheelZoomPresets is { Length: > 0 } presets
            ? ZoomPresetStepper.StepToNextPreset(basisZoom * 100f, presets, zoomIn ? +1 : -1)
            : basisZoom * 100f * (zoomIn ? 1.25f : 1f / 1.25f);
        return Math.Clamp(targetPct / 100f, CanvasTransform.MinZoom, CanvasTransform.MaxZoom);
    }

    private void StartZoomTimer()
    {
        _zoomTimer ??= CreateZoomTimer();
        _zoomTimer.Start();
    }

    private void StopZoomTimer() => _zoomTimer?.Stop();

    private DispatcherTimer CreateZoomTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ZoomAnimIntervalSeconds) };
        timer.Tick += (_, _) => StepZoomAnimation(ZoomAnimIntervalSeconds);
        return timer;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var props = e.GetCurrentPoint(this).Properties;
        var pos = e.GetPosition(this);

        if (PointerGestures.IsPanGesture(props.IsMiddleButtonPressed, props.IsLeftButtonPressed, e.KeyModifiers))
        {
            StartPan(pos);
            e.Pointer.Capture(this);
            return;
        }

        OnEditPointerPressed(e);
    }

    /// <summary>
    /// Editing hook fired from <see cref="OnPointerPressed"/> after the pan gesture is ruled out
    /// (not middle-mouse, not Alt+left). The base does nothing; editing subclasses implement
    /// selection / handle / create behavior here.
    /// </summary>
    protected virtual void OnEditPointerPressed(PointerPressedEventArgs e) { }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_isPanning)
        {
            UpdatePan((float)pos.X, (float)pos.Y);
            return;
        }

        OnEditPointerMoved(e);
    }

    /// <summary>
    /// Editing hook fired from <see cref="OnPointerMoved"/> when no pan is in progress. The base
    /// does nothing; editing subclasses implement drag / hover behavior here.
    /// </summary>
    protected virtual void OnEditPointerMoved(PointerEventArgs e) { }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            PanChanged?.Invoke(_panX, _panY);
            e.Pointer.Capture(null);
        }

        OnEditPointerReleased(e);
    }

    /// <summary>
    /// Ends an in-progress pan when capture is stolen (browser hosts often fire this without a
    /// matching <c>PointerReleased</c>). Subclasses that track their own drag state should
    /// override and clear that state too — see <c>WireframeControl</c>.
    /// </summary>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_isPanning)
        {
            _isPanning = false;
            PanChanged?.Invoke(_panX, _panY);
        }
    }

    /// <summary>
    /// Editing hook fired from <see cref="OnPointerReleased"/> after the pan gesture (if any) is
    /// ended. The base does nothing; editing subclasses commit drags / record undo here.
    /// </summary>
    protected virtual void OnEditPointerReleased(PointerReleasedEventArgs e) { }

    // ── Pan gesture ───────────────────────────────────────────────────────────

    private void StartPan(Point pos)
    {
        CancelZoomAnimation();   // panning takes over from any in-flight wheel ease
        _isPanning = true;
        _panAnchor = pos;
        _panAnchorX = _panX;   // camera pan at drag start
        _panAnchorY = _panY;
    }

    /// <summary>
    /// Free-pan: shifts the camera by the pointer displacement since the drag started, then
    /// clamps to the valid pan band. <paramref name="vpX"/>/<paramref name="vpY"/> are
    /// viewport-space (the control IS the viewport — no ScrollViewer offset to compensate).
    /// </summary>
    private void UpdatePan(float vpX, float vpY)
    {
        _panX = _panAnchorX + (vpX - (float)_panAnchor.X);
        _panY = _panAnchorY + (vpY - (float)_panAnchor.Y);
        ClampCamera();
        InvalidateVisual();
        RaiseViewChanged();
    }

    /// <summary>
    /// Zooms toward the viewport-space pivot (<paramref name="sx"/>, <paramref name="sy"/>) by
    /// <paramref name="factor"/>, preserving the texture coordinate under the pivot, then clamps
    /// the camera to the valid pan band. The clamp is the pure analytic
    /// <see cref="CanvasTransform.ZoomWireframe"/> — no dependency on a layout-resolved extent,
    /// so a symmetric zoom in/out round-trips and the reachable bounds at a given zoom are
    /// identical regardless of zoom direction (#422). Subsumes the old #138/#319/#341 point
    /// fixes: the texture is never pushed off-edge and is always pannable to the viewport centre.
    /// </summary>
    private void ZoomToward(float sx, float sy, float factor)
    {
        if (_bitmap == null || Bounds.Width <= 1)
        {
            (_panX, _panY, _zoom) = CanvasTransform.ZoomToward(sx, sy, factor, _panX, _panY, _zoom);
        }
        else
        {
            (_panX, _panY, _zoom) = CanvasTransform.ZoomWireframe(
                sx, sy, factor, _panX, _panY, _zoom,
                (float)Bounds.Width, (float)Bounds.Height,
                _bitmap.Width, _bitmap.Height);
        }

        InvalidateVisual();
        RaiseZoomChanged();
        RaiseViewChanged();
    }

    // ── Coordinate transforms ─────────────────────────────────────────────────

    protected SKPoint ScreenToTexture(float sx, float sy)
    {
        var (tx, ty) = CanvasTransform.ScreenToTexture(sx, sy, _panX, _panY, _zoom);
        return new SKPoint(tx, ty);
    }

    protected SKRect ToScreen(SKRect r)
    {
        var (l, t, rr, b) = CanvasTransform.TextureRectToScreen(
            r.Left, r.Top, r.Right, r.Bottom, _panX, _panY, _zoom);
        return new SKRect(l, t, rr, b);
    }

    // ── Centering ─────────────────────────────────────────────────────────────

    private void CenterTexture()
    {
        if (_bitmap is null) return;
        CancelZoomAnimation();   // a fresh texture/centre overrides any in-flight wheel ease

        // Defer until layout has produced a real viewport; the first SizeChanged re-centers.
        if (Bounds.Width <= 1)
        {
            _needsInitialCenter = true;
            return;
        }
        _needsInitialCenter = false;

        // CenterFit returns the top-left pan that centres the bitmap inside the viewport at
        // 85 % fit — exactly the wireframe's pan convention, so it can be used directly.
        (_panX, _panY, _zoom) = CanvasTransform.CenterFit(
            _bitmap.Width, _bitmap.Height, (float)Bounds.Width, (float)Bounds.Height);
        ClampCamera();

        InvalidateVisual();
        RaiseViewChanged();
    }
}
