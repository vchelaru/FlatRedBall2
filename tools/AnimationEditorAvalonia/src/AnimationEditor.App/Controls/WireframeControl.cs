using AnimationEditor.App.Theming;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
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
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Avalonia + SkiaSharp wireframe editor.
/// Replaces ImageRegionSelectionControl + WireframeManager from the WinForms port.
/// <para>
/// Coordinate systems:
///   Texture-space — pixel coords (0,0)→(W,H) inside the loaded bitmap.
///   Screen-space  — pixel coords within the control bounds (origin = top-left of control).
///   Transform: screenX = panX + textureX * zoom
/// </para>
/// </summary>
public class WireframeControl : Control
{
    // ── Inner types ───────────────────────────────────────────────────────────


    private sealed class FrameRect
    {
        public AnimationFrameSave Frame = null!;
        public SKRect Bounds;       // texture-space pixel coords
        public bool IsSelected;
    }

    /// <summary>
    /// Immutable snapshot of all rendering state, captured on the UI thread
    /// so the render thread can read it safely.
    /// </summary>
    private sealed class RenderSnapshot
    {
        // SKImage (not SKBitmap) — immutable and explicitly safe to read on the
        // Avalonia render thread while the UI thread holds the source bitmap.
        public SKImage? Image;
        public int ImageWidth, ImageHeight;
        public float PanX, PanY, Zoom;
        public bool ShowGrid;
        public int GridSize;
        public List<(SKRect Bounds, bool IsSelected)> Frames = new();
        public SKRect? SelectedHandleBounds;    // null → no handles drawn
        public bool ShowPreview;
        public SKRect PreviewRect;
        public double Width, Height;
        /// <summary>
        /// Texture-space position (pixels) of the entity origin for the selected frame.
        /// Null when no frame is selected or origin data is unavailable.
        /// </summary>
        public float? OriginTexX, OriginTexY;
    }

    private sealed class DrawOp : ICustomDrawOperation
    {
        private readonly RenderSnapshot _s;
        private readonly CanvasPalette _palette;

        public DrawOp(RenderSnapshot s, CanvasPalette palette) { _s = s; _palette = palette; Bounds = new Rect(0, 0, s.Width, s.Height); }

        public Rect Bounds { get; }
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext ctx)
        {
            var lease = ctx.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
            if (lease is null) return;
            using (lease)
                RenderSk(lease.SkCanvas, _s, _palette);
        }

        // ── Static rendering logic ────────────────────────────────────────────

        internal static void RenderSk(SKCanvas canvas, RenderSnapshot s, CanvasPalette palette)
        {
            canvas.Clear(palette.Background);

            if (s.Image != null)
            {
                var dest = new SKRect(
                    s.PanX, s.PanY,
                    s.PanX + s.ImageWidth * s.Zoom,
                    s.PanY + s.ImageHeight * s.Zoom);

                // Texture image — point sampling when zoomed ≥ 1× for pixel-art fidelity
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
                    DrawGrid(canvas, s, dest, palette.GridLine);
            }

            // Frame region rectangles
            using var frameFill = new SKPaint { Style = SKPaintStyle.Fill };
            using var frameStroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

            foreach (var (bounds, isSelected) in s.Frames)
            {
                var sr = ToScreen(bounds, s);
                if (isSelected)
                {
                    frameFill.Color = new SKColor(80, 160, 255, 45);
                    frameStroke.Color = new SKColor(80, 160, 255, 230);
                }
                else
                {
                    frameFill.Color = new SKColor(80, 160, 255, 18);
                    frameStroke.Color = new SKColor(80, 160, 255, 120);
                }
                canvas.DrawRect(sr, frameFill);
                canvas.DrawRect(sr, frameStroke);
            }

            // Resize handles on selected frame
            if (s.SelectedHandleBounds.HasValue)
                DrawHandles(canvas, ToScreen(s.SelectedHandleBounds.Value, s));

            // Magic-wand / grid-snap preview rectangle
            if (s.ShowPreview)
            {
                using var pvPaint = new SKPaint
                {
                    Color = new SKColor(255, 220, 0, 180),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.5f,
                    PathEffect = SKPathEffect.CreateDash(new float[] { 4f, 3f }, 0f)
                };
                canvas.DrawRect(ToScreen(s.PreviewRect, s), pvPaint);
            }

            // Origin crosshair — yellow cross at the entity (0,0) origin in texture space
            if (s.OriginTexX.HasValue && s.OriginTexY.HasValue)
            {
                float ox = s.PanX + s.OriginTexX.Value * s.Zoom;
                float oy = s.PanY + s.OriginTexY.Value * s.Zoom;
                const float ArmLen = 8f;
                using var crossPaint = new SKPaint
                {
                    Color       = new SKColor(255, 220, 0, 230),
                    Style       = SKPaintStyle.Stroke,
                    StrokeWidth = 1.5f,
                    IsAntialias = true
                };
                canvas.DrawLine(ox - ArmLen, oy, ox + ArmLen, oy, crossPaint);
                canvas.DrawLine(ox, oy - ArmLen, ox, oy + ArmLen, crossPaint);
                using var dotPaint = new SKPaint { Color = new SKColor(255, 220, 0, 230) };
                canvas.DrawCircle(ox, oy, 2f, dotPaint);
            }
        }

        private static void DrawGrid(SKCanvas canvas, RenderSnapshot s, SKRect textureDest, SKColor gridColor)
        {
            using var paint = new SKPaint
            {
                Color        = gridColor,
                Style        = SKPaintStyle.Stroke,
                StrokeWidth  = 0.5f,
                IsAntialias  = true
            };
            float step = s.GridSize * s.Zoom;

            for (float x = textureDest.Left + step; x < textureDest.Right; x += step)
                canvas.DrawLine(x, textureDest.Top, x, textureDest.Bottom, paint);

            for (float y = textureDest.Top + step; y < textureDest.Bottom; y += step)
                canvas.DrawLine(textureDest.Left, y, textureDest.Right, y, paint);
        }

        private const float Hs = 5f;  // Handle half-size: handles are drawn this far outside the frame edge

        private static void DrawHandles(SKCanvas canvas, SKRect sr)
        {
            using var fill = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
            using var stroke = new SKPaint { Color = SKColors.DodgerBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

            foreach (var pt in HandlePoints(sr))
            {
                var hr = new SKRect(pt.X - Hs, pt.Y - Hs, pt.X + Hs, pt.Y + Hs);
                canvas.DrawRect(hr, fill);
                canvas.DrawRect(hr, stroke);
            }
        }

        private static IEnumerable<SKPoint> HandlePoints(SKRect r)
        {
            float cx = r.MidX, cy = r.MidY;
            yield return new SKPoint(r.Left  - Hs, r.Top    - Hs);  // TopLeft
            yield return new SKPoint(cx,           r.Top    - Hs);  // TopCenter
            yield return new SKPoint(r.Right + Hs, r.Top    - Hs);  // TopRight
            yield return new SKPoint(r.Left  - Hs, cy);             // MidLeft
            yield return new SKPoint(r.Right + Hs, cy);             // MidRight
            yield return new SKPoint(r.Left  - Hs, r.Bottom + Hs);  // BotLeft
            yield return new SKPoint(cx,           r.Bottom + Hs);  // BotCenter
            yield return new SKPoint(r.Right + Hs, r.Bottom + Hs);  // BotRight
        }

        private static SKRect ToScreen(SKRect r, RenderSnapshot s)
        {
            var (l, t, rr, b) = CanvasTransform.TextureRectToScreen(
                r.Left, r.Top, r.Right, r.Bottom, s.PanX, s.PanY, s.Zoom);
            return new SKRect(l, t, rr, b);
        }
    }

    // ── Fields ────────────────────────────────────────────────────────────────

    private SKBitmap? _bitmap;
    // Immutable GPU-uploadable copy of _bitmap, built on the UI thread and
    // safe to draw from the Avalonia render thread.
    private SKImage? _image;
    private string? _loadedTexturePath;
    private InspectableImage? _inspectableImage;

    private float _zoom = 1f;
    // Camera pan: the screen position (within the control's viewport) of texture pixel (0,0).
    // screenX = panX + textureX * zoom. Clamped analytically by ClampCamera — there is no
    // ScrollViewer; the control IS the viewport and two ScrollBars are driven from this pan.
    private float _panX, _panY;

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

    private bool _showGrid;
    private int _gridSize = 16;

    private readonly List<FrameRect> _frameRects = new();

    // Set when LoadTexture/CenterTexture ran before the control had a real viewport
    // (Bounds not yet laid out); the first SizeChanged with valid Bounds re-centers.
    private bool _needsInitialCenter;

    // ── Debug tooling (toggle with F2 in the live app) ────────────────────────
    private bool _debugMode;
    private static readonly string _debugLogPath =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wireframe_debug.log");
    private static readonly Typeface _dbgTypeface = new("Consolas, Courier New");
    // ImmutableSolidColorBrush has no thread affinity and is safe to use from the compositor thread.
    private static readonly IImmutableBrush _dbgBg = new ImmutableSolidColorBrush(Color.FromArgb(210, 0, 0, 0));
    private static readonly IImmutableBrush _dbgFg = new ImmutableSolidColorBrush(Color.FromRgb(0, 255, 80));

    // Neutral canvas/grid/outline colors for the active theme variant. Refreshed from
    // ActualThemeVariant on every render and whenever the variant changes.
    private CanvasPalette _palette = CanvasPalette.Dark;

    /// <summary>
    /// Toggle the real-time debug overlay + event log.  Bind to F2 in MainWindow.
    /// Log is written to <see cref="DebugLogPath"/>.
    /// </summary>
    public void ToggleDebugMode()
    {
        _debugMode = !_debugMode;
        if (_debugMode)
        {
            File.WriteAllText(_debugLogPath,
                $"=== WireframeControl debug log {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n" +
                $"Repro: add anim → add frame → load sprite → grid on → zoom 100% → " +
                $"1 wheel notch → pan right\n\n");
            DebugLog("DEBUG_ON", $"log={_debugLogPath}");
        }
        else
        {
            DebugLog("DEBUG_OFF", "overlay hidden");
        }
        InvalidateVisual();
    }

    /// <summary>Path to the active debug event log file.</summary>
    public static string DebugLogPath => _debugLogPath;

    private void DebugLog(string category, string msg)
    {
        if (!_debugMode) return;
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{category,-14}] {msg}";
        File.AppendAllText(_debugLogPath, line + "\n");
    }

    private void DrawDebugOverlay(DrawingContext ctx)
    {
        if (!_debugMode) return;
        int bmpW = _bitmap?.Width  ?? 0;
        int bmpH = _bitmap?.Height ?? 0;

        var lines = new[]
        {
            "── WIREFRAME DEBUG (F2 to hide) ──",
            $"zoom          {_zoom * 100f,7:F1}%",
            $"panXY         X={_panX,7:F1}  Y={_panY:F1}",
            $"viewport      {Bounds.Width:F0} × {Bounds.Height:F0}",
            $"content       {bmpW * _zoom:F0} × {bmpH * _zoom:F0}",
            $"isPanning     {_isPanning}",
            "──────────────────────────────────",
            $"log: {System.IO.Path.GetFileName(_debugLogPath)}",
        };

        const double fsz  = 12;
        const double lineH = 15;
        const double padX  = 6;
        const double padY  = 4;
        double panelW = 310;
        double panelH = lines.Length * lineH + padY * 2;

        ctx.FillRectangle(_dbgBg, new Rect(0, 0, panelW, panelH));
        double y = padY;
        foreach (var line in lines)
        {
            ctx.DrawText(new FormattedText(
                line, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                _dbgTypeface, fsz, _dbgFg), new Point(padX, y));
            y += lineH;
        }
    }
    private bool _isPanning;
    private Point _panAnchor;
    private float _panAnchorX, _panAnchorY;

    private FrameRect? _draggingRect;
    private HandleKind _draggingHandle;
    private SKPoint _dragStartWorld;
    private SKRect _dragStartBounds;

    // Chain-drag state: set when the user drags the composite chain bounding rect
    private bool _draggingChain;
    private readonly List<(FrameRect Rect, SKRect StartBounds, float BL, float BT, float BR, float BB)> _chainDragStarts = new();

    // Bulk handle-drag state: populated at drag-start when multiple chains are selected.
    // Holds the before-state and start bounds of ALL visible frames so ApplyHandleDrag
    // can apply a uniform delta and OnPointerReleased can record a single undo entry.
    private readonly List<(FrameRect Rect, SKRect StartBounds, float BL, float BT, float BR, float BB)> _bulkHandleDragStarts = new();

    // Before-UV snapshot captured at drag start for undo recording
    private float _dragBeforeL, _dragBeforeT, _dragBeforeR, _dragBeforeB;

    // Preview rectangle (magic wand / grid snap hover)
    private bool _showPreview;
    private SKRect _previewRect;

    // Lazily-created "+" cursor shown when Ctrl is held and a click would add a frame.
    private static readonly Lazy<Cursor> _addFrameCursorLazy = new(CreateAddFrameCursor);
    private static Cursor AddFrameCursor => _addFrameCursorLazy.Value;

    // Per-texture saved camera (texture path → panX, panY, zoom). panX/panY are the screen
    // position of texture pixel (0,0) — the full camera pan in the analytic model.
    private readonly Dictionary<string, (float px, float py, float z)> _cameraByTexture = new();

    // ── Public properties ─────────────────────────────────────────────────────

    /// <summary>Absolute path of the currently displayed texture, or null.</summary>
    public string? LoadedTexturePath => _loadedTexturePath;

    /// <summary>Pixel dimensions of the loaded bitmap (0×0 when nothing is loaded).</summary>
    public (int Width, int Height) BitmapSize =>
        _bitmap is null ? (0, 0) : (_bitmap.Width, _bitmap.Height);

    /// <summary>Current zoom factor (1.0 = 100 %).</summary>
    public float Zoom => _zoom;

    private bool _isMagicWandMode;

    /// <summary>When true, mouse clicks perform a flood-fill to set/create the frame region.</summary>
    public bool IsMagicWandMode
    {
        get => _isMagicWandMode;
        set
        {
            _isMagicWandMode = value;
            if (value && _bitmap != null)
                _inspectableImage ??= new InspectableImage(_bitmap);
            if (!value)
                _showPreview = false;
            InvalidateVisual();
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired after a frame's UV coords have been updated by dragging a handle.</summary>
    public event Action<AnimationFrameSave>? FrameRegionChanged;

    /// <summary>
    /// Fired after all frames in a chain have been translated by dragging the chain's
    /// composite bounding rect on the wireframe. The payload is the chain whose frames moved.
    /// </summary>
    public event Action<AnimationChainSave>? ChainRegionChanged;

    /// <summary>
    /// Fired on every pointer move while dragging a handle (live update).
    /// Does NOT trigger save or tree refresh — use <see cref="FrameRegionChanged"/> for those.
    /// </summary>
    public event Action<AnimationFrameSave>? FrameLiveUpdated;

    /// <summary>
    /// Fired after every zoom change. Payload is the new zoom as a percentage (e.g. 100f = 100 %).
    /// </summary>
    public event Action<float>? ZoomChanged;

    /// <summary>Fired when the user finishes a pan gesture (pointer released after drag).</summary>
    public event Action<float, float>? PanChanged;

    /// <summary>
    /// When non-null, mouse-wheel zoom steps through these preset percentages instead of
    /// applying a raw ×1.25 / ÷1.25 multiplier.  Set by <c>MainWindow</c> on startup.
    /// <para>
    /// Standalone controls (no <c>MainWindow</c>) leave this null and retain the legacy
    /// multiplier behaviour, which is still useful in precision-zoom tests.
    /// </para>
    /// </summary>
    public int[]? WheelZoomPresets { get; set; }

    /// <summary>
    /// Fired when the user ctrl+clicks to add a new frame
    /// (minX, minY, maxX, maxY in texture pixel coords).
    /// </summary>
    public event Action<int, int, int, int>? FrameCreatedFromRegion;

    // ── Injected services ─────────────────────────────────────────────────────

    private ISelectedState? _selectedState;
    private IAppState? _appState;
    private IAppCommands? _appCommands;
    private IApplicationEvents? _events;
    private IProjectManager? _projectManager;
    private IUndoManager? _undoManager;

    /// <summary>
    /// Called from MainWindow after DI container wires all services.
    /// Moves subscriptions out of the constructor so services are available.
    /// </summary>
    public void InitializeServices(
        ISelectedState selectedState,
        IAppState appState,
        IAppCommands appCommands,
        IApplicationEvents events,
        IProjectManager projectManager,
        IUndoManager undoManager)
    {
        _selectedState   = selectedState;
        _appState        = appState;
        _appCommands     = appCommands;
        _events          = events;
        _projectManager  = projectManager;
        _undoManager     = undoManager;

        _selectedState.SelectionChanged     += () => Dispatcher.UIThread.InvokeAsync(RefreshAll);
        _appCommands.RefreshWireframeRequested += () => Dispatcher.UIThread.InvokeAsync(RefreshAll);
        _events.AchxLoaded                  += _ => Dispatcher.UIThread.InvokeAsync(RefreshAll);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public WireframeControl()
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

        // Stop the smooth-zoom timer if the control leaves the tree mid-animation.
        DetachedFromVisualTree += (_, _) => StopZoomTimer();

        // Subscriptions are deferred to InitializeServices (called from MainWindow)
    }

    // ── Camera clamping + scrollbar integration ───────────────────────────────

    /// <summary>
    /// Fired whenever the camera (pan, zoom) or the viewport size changes, so the host can
    /// refresh the wireframe's two <c>ScrollBar</c>s from <see cref="GetScrollBarRanges"/>.
    /// Distinct from <see cref="PanChanged"/>, which fires only on pan-gesture completion for
    /// persistence.
    /// </summary>
    public event Action? ViewChanged;

    private void RaiseViewChanged() => ViewChanged?.Invoke();

    /// <summary>
    /// Clamps the camera pan so the texture's far edge can reach the viewport centre but no
    /// further — the texture is never scrolled fully out of view, yet any texture point can be
    /// brought to the centre. Pure analytic clamp (<see cref="CanvasTransform.ClampWireframePan"/>)
    /// — no ScrollViewer extent dependency, which is what makes a symmetric zoom in/out an exact
    /// round-trip (#422). No-op until layout has produced a real viewport (<c>Bounds.Width &gt; 1</c>)
    /// or with no texture.
    /// </summary>
    private void ClampCamera()
    {
        if (_bitmap == null || Bounds.Width <= 1) return;
        (_panX, _panY) = CanvasTransform.ClampWireframePan(
            _panX, _panY, (float)Bounds.Width, (float)Bounds.Height,
            _bitmap.Width, _bitmap.Height, _zoom);
    }

    /// <summary>
    /// Scrollbar (Minimum, Maximum, Value, ViewportSize) for each axis, derived from the
    /// current pan, viewport, and texture size. <c>MainWindow</c> applies these to the two
    /// wireframe <c>ScrollBar</c>s; the value axis is the negation of the pan axis (see
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
    /// Load a PNG from disk and show it. Pass null to clear the view.
    /// Saves the camera position for the old texture and restores it for the new one.
    /// </summary>
    public void LoadTexture(string? filePath)
    {
        // Lowercased + slash-normalized form used only for cache-key comparison and the
        // _loadedTexturePath identity that downstream filter code keys on. The case-preserving
        // form is what actually goes to the filesystem (Linux is case-sensitive).
        string? norm = string.IsNullOrEmpty(filePath) ? null : new FilePath(filePath).Standardized;
        string? casePreserved = string.IsNullOrEmpty(filePath) ? null : new FilePath(filePath).StandardizedCaseSensitive;

        if (_loadedTexturePath == norm)
        {
            // Texture hasn't changed, but the selected frame may have, so update frame rects.
            RefreshFramesInternal();
            return;
        }

        // Save camera for the texture we're leaving
        if (_loadedTexturePath != null)
            _cameraByTexture[_loadedTexturePath] = (_panX, _panY, _zoom);

        _loadedTexturePath = norm;
        _image?.Dispose();
        _image = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _inspectableImage = null;

        if (casePreserved != null && File.Exists(casePreserved))
        {
            _bitmap = SKBitmap.Decode(casePreserved);
            // Upload pixels into an immutable SKImage on the UI thread so the
            // render thread never touches the SKBitmap directly. Without this,
            // SKCanvas.DrawBitmap on the render thread crashes with AV.
            _image = SKImage.FromBitmap(_bitmap);

            if (_isMagicWandMode)
                _inspectableImage = new InspectableImage(_bitmap);

            if (_cameraByTexture.TryGetValue(norm!, out var cam))
            {
                // Restore the full camera and re-clamp against the current viewport (window may
                // have been resized since this texture was last shown).
                (_panX, _panY, _zoom) = (cam.px, cam.py, cam.z);
                ClampCamera();
                RaiseViewChanged();
                InvalidateVisual();
            }
            else
            {
                CenterTexture();
            }
        }

        RefreshFramesInternal();
    }

    /// <summary>
    /// Rebuild the displayed frame rectangles from SelectedState
    /// (must be called on the UI thread).
    /// </summary>
    public void RefreshFrames() => RefreshFramesInternal();

    /// <summary>Number of frame rects currently visible in the wireframe. For tests only.</summary>
    public int FrameRectCount => _frameRects.Count;

    /// <summary>Re-detect the current texture from the selection, reload it, and refresh frames.</summary>
    public void RefreshAll()
    {
        var path = DetermineTexturePath();
        LoadTexture(path);
    }

    /// <summary>
    /// Force-reload the currently displayed texture from disk, bypassing the identity check.
    /// Use for PNG hot-reload when the file content changed but the path did not.
    /// Must be called on the UI thread.
    /// </summary>
    public void ForceReloadTexture()
    {
        var path = _loadedTexturePath;
        if (path == null) return;
        _loadedTexturePath = null;   // clear identity so LoadTexture doesn't short-circuit
        LoadTexture(new FilePath(path).StandardizedCaseSensitive);
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
        RefreshFramesInternal();
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

    /// <summary>
    /// Fires <see cref="FrameCreatedFromRegion"/> with the grid-snapped cell
    /// that contains the given screen position. Guards match production code:
    /// no-ops when bitmap is null, grid is off, or cell size is ≤ 0.
    /// </summary>
    public void SimulateGridSnapClick(float screenX, float screenY)
    {
        if (_bitmap is null || !_showGrid || _gridSize <= 0) return;
        var world = ScreenToTexture(screenX, screenY);
        int gx = GridSnapper.Snap(world.X, _gridSize);
        int gy = GridSnapper.Snap(world.Y, _gridSize);
        FrameCreatedFromRegion?.Invoke(gx, gy, gx + _gridSize, gy + _gridSize);
    }

    /// <summary>
    /// Applies the grid-snapped cell at the given screen position to the currently-selected frame
    /// (via <see cref="ApplyRegionToSelectedFrame"/>). This is the double-click path in grid mode:
    /// it bypasses handle hit-testing so frames that cover the entire texture can still be
    /// assigned a specific cell.
    /// No-ops when bitmap is null, grid is off, cell size is ≤ 0, or no frame is selected.
    /// </summary>
    public void SimulateGridSnapDoubleClick(float screenX, float screenY)
    {
        if (_bitmap is null || !_showGrid || _gridSize <= 0) return;
        var world = ScreenToTexture(screenX, screenY);
        int gx = GridSnapper.Snap(world.X, _gridSize);
        int gy = GridSnapper.Snap(world.Y, _gridSize);
        ApplyRegionToSelectedFrame(gx, gy, gx + _gridSize, gy + _gridSize);
    }

    /// <summary>
    /// Runs the hover-preview snap logic for the given screen point and returns
    /// the resulting preview state. Requires a loaded texture (returns ShowPreview=false otherwise).
    /// </summary>
    public (bool ShowPreview, SKRect PreviewRect) GetPreviewStateForScreenPoint(float screenX, float screenY)
    {
        UpdatePreview(new Point(screenX, screenY));
        return (_showPreview, _previewRect);
    }

    /// <summary>
    /// Simulates a complete handle-drag gesture on the currently-selected frame,
    /// from <paramref name="startScreenX"/>,<paramref name="startScreenY"/> to
    /// <paramref name="endScreenX"/>,<paramref name="endScreenY"/> in screen space.
    /// <para>
    /// Drives the same <see cref="ApplyHandleDrag"/> code path as real pointer events,
    /// writes updated UV coordinates back to the frame, and fires
    /// <see cref="FrameRegionChanged"/>. No-op when no frame is selected or no texture
    /// is loaded.
    /// </para>
    /// </summary>
    public void SimulateHandleDrag(HandleKind handle,
        float startScreenX, float startScreenY,
        float endScreenX,   float endScreenY)
    {
        var sel = _frameRects.FirstOrDefault(f => f.IsSelected);
        if (sel is null || _bitmap is null) return;

        _draggingRect    = sel;
        _draggingHandle  = handle;
        _dragStartWorld  = ScreenToTexture(startScreenX, startScreenY);
        _dragStartBounds = sel.Bounds;
        _dragBeforeL = sel.Frame.LeftCoordinate;
        _dragBeforeT = sel.Frame.TopCoordinate;
        _dragBeforeR = sel.Frame.RightCoordinate;
        _dragBeforeB = sel.Frame.BottomCoordinate;
        _bulkHandleDragStarts.Clear();

        ApplyHandleDrag(new Point(endScreenX, endScreenY));

        float aL = sel.Frame.LeftCoordinate, aT = sel.Frame.TopCoordinate;
        float aR = sel.Frame.RightCoordinate, aB = sel.Frame.BottomCoordinate;
        if (RegionChanged(_dragBeforeL, _dragBeforeT, _dragBeforeR, _dragBeforeB, aL, aT, aR, aB))
        {
            FrameRegionChanged?.Invoke(sel.Frame);
            _undoManager!.Record(new FrameRegionChangedCommand(
                sel.Frame,
                _dragBeforeL, _dragBeforeT, _dragBeforeR, _dragBeforeB,
                aL, aT, aR, aB,
                _appCommands!, _events!));
        }
        _draggingRect   = null;
        _draggingHandle = HandleKind.None;
    }

    /// <summary>
    /// Test-only: simulates a bulk handle-drag gesture for multi-chain mode.
    /// Applies the same handle delta to <paramref name="targetFrame"/> and every
    /// other visible frame rect, then records a single <see cref="BulkFrameRegionChangedCommand"/>.
    /// No-op when the target frame is not found in the visible rects, no bitmap is loaded,
    /// or fewer than two frame rects are visible.
    /// </summary>
    public void SimulateBulkHandleDrag(AnimationFrameSave targetFrame,
        HandleKind handle,
        float startScreenX, float startScreenY,
        float endScreenX,   float endScreenY)
    {
        var primary = _frameRects.FirstOrDefault(fr => fr.Frame == targetFrame);
        if (primary is null || _bitmap is null) return;

        _draggingRect    = primary;
        _draggingHandle  = handle;
        _dragStartWorld  = ScreenToTexture(startScreenX, startScreenY);
        _dragStartBounds = primary.Bounds;
        _dragBeforeL = primary.Frame.LeftCoordinate;
        _dragBeforeT = primary.Frame.TopCoordinate;
        _dragBeforeR = primary.Frame.RightCoordinate;
        _dragBeforeB = primary.Frame.BottomCoordinate;

        _bulkHandleDragStarts.Clear();
        foreach (var fr in _frameRects)
            _bulkHandleDragStarts.Add((fr, fr.Bounds,
                fr.Frame.LeftCoordinate, fr.Frame.TopCoordinate,
                fr.Frame.RightCoordinate, fr.Frame.BottomCoordinate));

        ApplyHandleDrag(new Point(endScreenX, endScreenY));

        var snapshots = _bulkHandleDragStarts
            .Select(s => new BulkFrameRegionChangedCommand.FrameSnapshot(
                s.Rect.Frame,
                s.BL, s.BT, s.BR, s.BB,
                s.Rect.Frame.LeftCoordinate, s.Rect.Frame.TopCoordinate,
                s.Rect.Frame.RightCoordinate, s.Rect.Frame.BottomCoordinate))
            .ToList();
        if (snapshots.Any(s => RegionChanged(s.BL, s.BT, s.BR, s.BB, s.AL, s.AT, s.AR, s.AB)))
        {
            _undoManager!.Record(new BulkFrameRegionChangedCommand(snapshots, _appCommands!, _events!));
            foreach (var (fr, _, _, _, _, _) in _bulkHandleDragStarts)
                FrameRegionChanged?.Invoke(fr.Frame);
        }

        _bulkHandleDragStarts.Clear();
        _draggingRect   = null;
        _draggingHandle = HandleKind.None;
    }

    /// <summary>
    /// Simulates a complete chain-drag gesture: translates all frames of the currently-selected
    /// chain from <paramref name="startScreenX"/>,<paramref name="startScreenY"/> to
    /// <paramref name="endScreenX"/>,<paramref name="endScreenY"/> in screen space.
    /// <para>
    /// Drives the same <see cref="ApplyChainDrag"/> code path as real pointer events,
    /// writes updated UV coordinates back to every frame, and fires
    /// <see cref="ChainRegionChanged"/>. No-op when no chain is selected, no frames
    /// are visible, or no texture is loaded.
    /// </para>
    /// </summary>
    public void SimulateChainDrag(
        float startScreenX, float startScreenY,
        float endScreenX,   float endScreenY)
    {
        var chain = _selectedState?.SelectedChain;
        if (chain is null || _bitmap is null || _frameRects.Count == 0) return;

        _draggingChain = true;
        _chainDragStarts.Clear();
        foreach (var fr in _frameRects)
            _chainDragStarts.Add((fr, fr.Bounds,
                fr.Frame.LeftCoordinate, fr.Frame.TopCoordinate,
                fr.Frame.RightCoordinate, fr.Frame.BottomCoordinate));
        _dragStartWorld = ScreenToTexture(startScreenX, startScreenY);

        ApplyChainDrag(new Point(endScreenX, endScreenY));

        if (_chainDragStarts.Count > 0)
        {
            var snapshots = _chainDragStarts
                .Select(s => new BulkFrameRegionChangedCommand.FrameSnapshot(
                    s.Rect.Frame,
                    s.BL, s.BT, s.BR, s.BB,
                    s.Rect.Frame.LeftCoordinate, s.Rect.Frame.TopCoordinate,
                    s.Rect.Frame.RightCoordinate, s.Rect.Frame.BottomCoordinate))
                .ToList();
            if (snapshots.Any(s => RegionChanged(s.BL, s.BT, s.BR, s.BB, s.AL, s.AT, s.AR, s.AB)))
                _undoManager!.Record(new BulkFrameRegionChangedCommand(snapshots, _appCommands!, _events!));
        }

        ChainRegionChanged?.Invoke(chain);
        _draggingChain = false;
        _chainDragStarts.Clear();
    }

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
        ctx.Custom(new DrawOp(snap, _palette));
        DrawDebugOverlay(ctx);
    }

    // ActualThemeVariant resolves Default to the concrete platform variant, so a simple
    // "is it Light?" check correctly handles the follow-system case.
    private void UpdatePalette() => _palette = CanvasPalette.For(ActualThemeVariant != ThemeVariant.Light);

    /// <summary>
    /// Renders the current wireframe state to an off-screen bitmap of the given size.
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
    /// Pans so <paramref name="frame"/>'s centre lands at the viewport centre, preserving the
    /// current zoom level (the zoom is never changed, so <see cref="ZoomChanged"/> does not fire).
    /// Clamped to the valid pan band, so a frame near the texture edge lands as close to centre as
    /// the dead-space allows. Does nothing when no bitmap is loaded.
    /// </summary>
    public void CenterOnFrame(AnimationFrameSave frame)
    {
        if (_bitmap is null) return;
        CancelZoomAnimation();   // double-click centring overrides any in-flight wheel ease

        float bmpW = _bitmap.Width;
        float bmpH = _bitmap.Height;

        float pixL = frame.LeftCoordinate  * bmpW;
        float pixT = frame.TopCoordinate   * bmpH;
        float pixR = frame.RightCoordinate * bmpW;
        float pixB = frame.BottomCoordinate * bmpH;

        float texCX = (pixL + pixR) / 2f;
        float texCY = (pixT + pixB) / 2f;

        float vpW = (float)Bounds.Width;
        float vpH = (float)Bounds.Height;

        // Pan so the frame centre maps to the viewport centre at the current zoom
        // (screenX = panX + texX*zoom). The zoom is left untouched.
        _panX = vpW / 2f - texCX * _zoom;
        _panY = vpH / 2f - texCY * _zoom;
        ClampCamera();

        InvalidateVisual();
        RaiseViewChanged();
    }

    /// <summary>
    /// Returns a snapshot of the frame rectangles currently tracked by the control.
    /// Bounds are in texture-space pixel coordinates; call after
    /// <see cref="RefreshFrames"/> to ensure the list is current.
    /// </summary>
    public IReadOnlyList<(SKRect Bounds, bool IsSelected)> GetFrameRects() =>
        _frameRects.Select(fr => (fr.Bounds, fr.IsSelected)).ToList();

    private RenderSnapshot BuildSnapshot(double width, double height)
    {
        var snap = new RenderSnapshot
        {
            Image        = _image,
            ImageWidth   = _bitmap?.Width ?? 0,
            ImageHeight  = _bitmap?.Height ?? 0,
            PanX         = _panX,
            PanY         = _panY,
            Zoom         = _zoom,
            ShowGrid     = _showGrid,
            GridSize     = _gridSize,
            Width        = width,
            Height       = height,
            ShowPreview  = _showPreview,
            PreviewRect  = _previewRect,
        };

        foreach (var fr in _frameRects)
            snap.Frames.Add((fr.Bounds, fr.IsSelected));

        var sel = _frameRects.FirstOrDefault(f => f.IsSelected);
        if (sel != null && !_isMagicWandMode)
        {
            snap.SelectedHandleBounds = sel.Bounds;

            // Compute the entity-origin position in texture space.
            // frame.Bounds are already in texture pixels; the entity's (0,0) is offset
            // from the frame's center by (-RelativeX, +RelativeY) in game space
            // (RelativeX/Y stored in display pixels = stored * OffsetMultiplier).
            // Game Y+ = screen up = texture row decreasing → negate RelativeY.
            float offMult = _appState?.OffsetMultiplier ?? 1f;
            snap.OriginTexX = sel.Bounds.MidX - sel.Frame.RelativeX * offMult;
            snap.OriginTexY = sel.Bounds.MidY + sel.Frame.RelativeY * offMult;
        }
        // Chain selected (no individual frame): handles are not rendered.
        // Move-drag still works via HitTestHandle, which uses _frameRects directly.

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
    private void CancelZoomAnimation()
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
        bool isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        bool isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;

        // Middle-mouse or Alt+left → pan
        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && isAlt))
        {
            StartPan(pos);
            e.Pointer.Capture(this);
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        // Grid mode double-click: bypass handle hit-testing so that a frame covering
        // the entire texture (which would otherwise always hit HandleKind.Move) can still
        // have a specific grid cell applied to it.
        if (!isCtrl && !_isMagicWandMode && e.ClickCount == 2 && _showGrid && _gridSize > 0 && _bitmap != null)
        {
            var dblWorld = ScreenToTexture((float)pos.X, (float)pos.Y);
            int gx = GridSnapper.Snap(dblWorld.X, _gridSize);
            int gy = GridSnapper.Snap(dblWorld.Y, _gridSize);
            ApplyRegionToSelectedFrame(gx, gy, gx + _gridSize, gy + _gridSize);
            return;
        }

        // 1. Hit-test resize handles on the selected frame (skipped in Magic Wand mode)
        if (!isCtrl && !_isMagicWandMode)
        {
            var (hitFrame, hitHandle) = HitTestHandle(pos);
            if (hitHandle != HandleKind.None)
            {
                if (hitFrame != null)
                {
                    // Single-frame drag (or bulk handle drag when multi-chain selected)
                    _draggingRect = hitFrame;
                    _draggingHandle = hitHandle;
                    _dragStartWorld = ScreenToTexture((float)pos.X, (float)pos.Y);
                    _dragStartBounds = hitFrame.Bounds;
                    _dragBeforeL = hitFrame.Frame.LeftCoordinate;
                    _dragBeforeT = hitFrame.Frame.TopCoordinate;
                    _dragBeforeR = hitFrame.Frame.RightCoordinate;
                    _dragBeforeB = hitFrame.Frame.BottomCoordinate;

                    // In multi-chain mode, capture before-state of ALL visible frames for
                    // bulk apply and a single atomic undo command.
                    _bulkHandleDragStarts.Clear();
                    if ((_selectedState?.SelectedChains?.Count ?? 0) > 1)
                    {
                        foreach (var fr in _frameRects)
                            _bulkHandleDragStarts.Add((fr, fr.Bounds,
                                fr.Frame.LeftCoordinate, fr.Frame.TopCoordinate,
                                fr.Frame.RightCoordinate, fr.Frame.BottomCoordinate));
                    }
                }
                else
                {
                    // Chain drag: move all chain frames together
                    _draggingChain = true;
                    _chainDragStarts.Clear();
                    foreach (var fr in _frameRects)
                        _chainDragStarts.Add((fr, fr.Bounds,
                            fr.Frame.LeftCoordinate, fr.Frame.TopCoordinate,
                            fr.Frame.RightCoordinate, fr.Frame.BottomCoordinate));
                    _dragStartWorld = ScreenToTexture((float)pos.X, (float)pos.Y);
                }
                e.Pointer.Capture(this);
                return;
            }
        }

        if (_bitmap is null) return;

        var world = ScreenToTexture((float)pos.X, (float)pos.Y);

        // 2. Magic-wand mode
        if (_isMagicWandMode && _inspectableImage != null)
        {
            if (isCtrl)
            {
                // Ctrl+click: create a new frame from the wand's flood-fill bounds.
                _inspectableImage.GetOpaqueWandBounds(
                    (int)world.X, (int)world.Y,
                    out int minX, out int minY, out int maxX, out int maxY);
                if (maxX >= minX && maxY >= minY)
                    FrameCreatedFromRegion?.Invoke(minX, minY, maxX, maxY);
            }
            else if (e.ClickCount >= 2 && _showPreview)
            {
                // Double-click: apply the currently-hovered preview rect to the selected frame.
                ApplyPreviewToSelectedFrame();
            }
            else
            {
                // Single-click: plain frame selection only.
                TrySelectFrameAtPoint(world);
            }
            return;
        }

        // 3. Grid mode: Ctrl+click → create frame; plain click → set region of selected frame
        if (_showGrid && _gridSize > 0)
        {
            int gx = GridSnapper.Snap(world.X, _gridSize);
            int gy = GridSnapper.Snap(world.Y, _gridSize);
            if (isCtrl)
                FrameCreatedFromRegion?.Invoke(gx, gy, gx + _gridSize, gy + _gridSize);
            else
                ApplyRegionToSelectedFrame(gx, gy, gx + _gridSize, gy + _gridSize);
            return;
        }

        // 4. Plain mode: Ctrl+click → create a new frame centered at the click point;
        //    plain click → select the frame under the cursor.
        if (isCtrl)
        {
            var (lastW, lastH) = GetLastFramePixelSize();
            var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(
                world.X, world.Y, _bitmap.Width, _bitmap.Height, lastW, lastH);
            FrameCreatedFromRegion?.Invoke(minX, minY, maxX, maxY);
            return;
        }

        TrySelectFrameAtPoint(world);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_isPanning)
        {
            UpdatePan((float)pos.X, (float)pos.Y);
            return;
        }

        if (_draggingRect != null)
        {
            ApplyHandleDrag(pos);
            return;
        }

        if (_draggingChain)
        {
            ApplyChainDrag(pos);
            return;
        }

        UpdateHoverCursor(pos, isCtrl: (e.KeyModifiers & KeyModifiers.Control) != 0);

        // Update hover preview for magic-wand / grid-snap
        UpdatePreview(pos);
    }

    private void UpdateHoverCursor(Point pos, bool isCtrl = false)
    {
        // When Ctrl is held and a bitmap is loaded, any click will create a new frame.
        if (isCtrl && _bitmap != null)
        {
            Cursor = AddFrameCursor;
            return;
        }

        var (_, hitHandle) = HitTestHandle(pos);
        var cursorType = HandleCursorMapper.CursorTypeFor(hitHandle);
        Cursor = cursorType is null
            ? Cursor.Default
            : new Cursor(cursorType.Value);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            PanChanged?.Invoke(_panX, _panY);
            e.Pointer.Capture(null);
        }

        if (_draggingRect != null)
        {
            if (_bulkHandleDragStarts.Count > 0)
            {
                // Bulk drag: record one atomic undo command covering all affected frames,
                // then notify listeners for each changed frame.
                var snapshots = _bulkHandleDragStarts
                    .Select(s => new BulkFrameRegionChangedCommand.FrameSnapshot(
                        s.Rect.Frame,
                        s.BL, s.BT, s.BR, s.BB,
                        s.Rect.Frame.LeftCoordinate, s.Rect.Frame.TopCoordinate,
                        s.Rect.Frame.RightCoordinate, s.Rect.Frame.BottomCoordinate))
                    .ToList();
                if (snapshots.Any(s => RegionChanged(s.BL, s.BT, s.BR, s.BB, s.AL, s.AT, s.AR, s.AB)))
                {
                    _undoManager!.Record(new BulkFrameRegionChangedCommand(snapshots, _appCommands!, _events!));
                    foreach (var (fr, _, _, _, _, _) in _bulkHandleDragStarts)
                        FrameRegionChanged?.Invoke(fr.Frame);
                }
                _bulkHandleDragStarts.Clear();
            }
            else
            {
                float aL = _draggingRect.Frame.LeftCoordinate;
                float aT = _draggingRect.Frame.TopCoordinate;
                float aR = _draggingRect.Frame.RightCoordinate;
                float aB = _draggingRect.Frame.BottomCoordinate;
                if (RegionChanged(_dragBeforeL, _dragBeforeT, _dragBeforeR, _dragBeforeB, aL, aT, aR, aB))
                {
                    FrameRegionChanged?.Invoke(_draggingRect.Frame);
                    _undoManager!.Record(new FrameRegionChangedCommand(
                        _draggingRect.Frame,
                        _dragBeforeL, _dragBeforeT, _dragBeforeR, _dragBeforeB,
                        aL, aT, aR, aB,
                        _appCommands!, _events!));
                }
            }
            _draggingRect = null;
            _draggingHandle = HandleKind.None;
            e.Pointer.Capture(null);
        }

        if (_draggingChain)
        {
            var chain = _selectedState!.SelectedChain;
            if (chain != null)
            {
                if (_chainDragStarts.Count > 0)
                {
                    var snapshots = _chainDragStarts
                        .Select(s => new BulkFrameRegionChangedCommand.FrameSnapshot(
                            s.Rect.Frame,
                            s.BL, s.BT, s.BR, s.BB,
                            s.Rect.Frame.LeftCoordinate, s.Rect.Frame.TopCoordinate,
                            s.Rect.Frame.RightCoordinate, s.Rect.Frame.BottomCoordinate))
                        .ToList();
                    if (snapshots.Any(s => RegionChanged(s.BL, s.BT, s.BR, s.BB, s.AL, s.AT, s.AR, s.AB)))
                        _undoManager!.Record(new BulkFrameRegionChangedCommand(snapshots, _appCommands!, _events!));
                }
                ChainRegionChanged?.Invoke(chain);
            }
            _draggingChain = false;
            _chainDragStarts.Clear();
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Cursor = Cursor.Default;
        if (_showPreview) { _showPreview = false; InvalidateVisual(); }
    }

    // ── Mouse helpers ─────────────────────────────────────────────────────────

    private static bool RegionChanged(
        float bL, float bT, float bR, float bB,
        float aL, float aT, float aR, float aB)
        => Math.Abs(aL - bL) > 0.0001f || Math.Abs(aT - bT) > 0.0001f ||
           Math.Abs(aR - bR) > 0.0001f || Math.Abs(aB - bB) > 0.0001f;

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
        ZoomChanged?.Invoke(_zoom * 100f);
        RaiseViewChanged();
    }

    private void ApplyHandleDrag(Point pos)
    {
        if (_draggingRect is null || _bitmap is null) return;

        var world = ScreenToTexture((float)pos.X, (float)pos.Y);
        float dx = world.X - _dragStartWorld.X;
        float dy = world.Y - _dragStartWorld.Y;
        var startBounds = new BoundsRect(_dragStartBounds.Left, _dragStartBounds.Top,
                                         _dragStartBounds.Right, _dragStartBounds.Bottom);

        var nb = DragHandleApplier.Apply(_draggingHandle, dx, dy, startBounds);

        // Always snap to integer pixel; upgrade to grid-size snap when the grid is on.
        int snapSize = (_showGrid && _gridSize > 0) ? _gridSize : 1;
        nb = DragHandleApplier.SnapEdges(nb, _draggingHandle, snapSize);

        _draggingRect.Bounds = new SKRect(nb.Left, nb.Top, nb.Right, nb.Bottom);

        // Write UV coords back to the primary frame
        var (l, t, r, b) = DragHandleApplier.ToUvCoords(nb, _bitmap.Width, _bitmap.Height);
        var f = _draggingRect.Frame;
        f.LeftCoordinate   = l;
        f.RightCoordinate  = r;
        f.TopCoordinate    = t;
        f.BottomCoordinate = b;

        // Apply the same delta to all other frames in bulk mode
        if (_bulkHandleDragStarts.Count > 0)
        {
            float texW = _bitmap.Width, texH = _bitmap.Height;
            foreach (var (fr, startB, _, _, _, _) in _bulkHandleDragStarts)
            {
                if (fr == _draggingRect) continue;
                var sb = new BoundsRect(startB.Left, startB.Top, startB.Right, startB.Bottom);
                var nb2 = DragHandleApplier.Apply(_draggingHandle, dx, dy, sb);
                nb2 = DragHandleApplier.SnapEdges(nb2, _draggingHandle, snapSize);
                fr.Bounds = new SKRect(nb2.Left, nb2.Top, nb2.Right, nb2.Bottom);
                var (l2, t2, r2, b2) = DragHandleApplier.ToUvCoords(nb2, texW, texH);
                fr.Frame.LeftCoordinate   = l2;
                fr.Frame.RightCoordinate  = r2;
                fr.Frame.TopCoordinate    = t2;
                fr.Frame.BottomCoordinate = b2;
            }
        }

        // Live update for the property panel (no save / tree refresh yet)
        FrameLiveUpdated?.Invoke(_draggingRect.Frame);
        InvalidateVisual();
    }

    private void ApplyChainDrag(Point pos)
    {
        if (!_draggingChain || _bitmap is null) return;

        var world = ScreenToTexture((float)pos.X, (float)pos.Y);
        float dx = world.X - _dragStartWorld.X;
        float dy = world.Y - _dragStartWorld.Y;

        // Snap to integer pixel; upgrade to grid-size snap when the grid is on.
        int snapSize = (_showGrid && _gridSize > 0) ? _gridSize : 1;
        dx = MathF.Round(dx / snapSize) * snapSize;
        dy = MathF.Round(dy / snapSize) * snapSize;

        float texW = _bitmap.Width;
        float texH = _bitmap.Height;

        foreach (var (fr, startBounds, _, _, _, _) in _chainDragStarts)
        {
            float newL = startBounds.Left   + dx;
            float newT = startBounds.Top    + dy;
            float newR = startBounds.Right  + dx;
            float newB = startBounds.Bottom + dy;

            fr.Bounds = new SKRect(newL, newT, newR, newB);
            fr.Frame.LeftCoordinate   = newL / texW;
            fr.Frame.TopCoordinate    = newT / texH;
            fr.Frame.RightCoordinate  = newR / texW;
            fr.Frame.BottomCoordinate = newB / texH;
        }

        InvalidateVisual();
    }

    private SKRect ComputeChainBoundingRect()
    {
        float l = float.MaxValue, t = float.MaxValue;
        float r = float.MinValue, b = float.MinValue;
        foreach (var fr in _frameRects)
        {
            l = MathF.Min(l, fr.Bounds.Left);
            t = MathF.Min(t, fr.Bounds.Top);
            r = MathF.Max(r, fr.Bounds.Right);
            b = MathF.Max(b, fr.Bounds.Bottom);
        }
        return new SKRect(l, t, r, b);
    }

    private void UpdatePreview(Point pos)
    {
        if (_bitmap is null) { ClearPreview(); return; }

        var world = ScreenToTexture((float)pos.X, (float)pos.Y);

        if (_isMagicWandMode && _inspectableImage != null)
        {
            _inspectableImage.GetOpaqueWandBounds(
                (int)world.X, (int)world.Y,
                out int minX, out int minY, out int maxX, out int maxY);

            bool found = maxX >= minX && maxY >= minY;
            _showPreview = found;
            if (found) _previewRect = new SKRect(minX, minY, maxX, maxY);
            InvalidateVisual();
        }
        else
        {
            ClearPreview();
        }
    }

    private void ClearPreview()
    {
        if (_showPreview) { _showPreview = false; InvalidateVisual(); }
    }

    private (FrameRect? frame, HandleKind handle) HitTestHandle(Point pos)
    {
        var sel = _frameRects.FirstOrDefault(f => f.IsSelected);
        if (sel != null)
        {
            var sr = ToScreen(sel.Bounds);

            var kind = DragHandleHitTester.GetHandleAt(
                (float)pos.X, (float)pos.Y,
                sr.Left, sr.Top, sr.Right, sr.Bottom,
                handleOffset: 5f);  // matches Hs: handles drawn outside the frame by this amount

            return kind == HandleKind.None ? (null, HandleKind.None) : (sel, kind);
        }

        // Multi-chain mode: test handles on every individual frame rect so bulk resize
        // (uniform delta) is accessible even when no single frame is selected.
        if ((_selectedState?.SelectedChains?.Count ?? 0) > 1 && _frameRects.Count > 0)
        {
            foreach (var fr in _frameRects)
            {
                var sr = ToScreen(fr.Bounds);
                var kind = DragHandleHitTester.GetHandleAt(
                    (float)pos.X, (float)pos.Y,
                    sr.Left, sr.Top, sr.Right, sr.Bottom,
                    handleOffset: 5f);
                if (kind != HandleKind.None)
                    return (fr, kind);
            }
            // No per-frame handle hit — fall through to composite Move.
        }

        // Single chain or multi-chain composite Move handle.
        // All hits are treated as Move since resizing the group via the bounding rect is not supported.
        if (_selectedState?.SelectedChain != null && _frameRects.Count > 0)
        {
            var chainRect = ComputeChainBoundingRect();
            var sr = ToScreen(chainRect);

            var kind = DragHandleHitTester.GetHandleAt(
                (float)pos.X, (float)pos.Y,
                sr.Left, sr.Top, sr.Right, sr.Bottom,
                handleOffset: 5f);

            if (kind != HandleKind.None)
                return (null, HandleKind.Move);
        }

        return (null, HandleKind.None);
    }

    private void TrySelectFrameAtPoint(SKPoint worldPt)
    {
        foreach (var fr in _frameRects)
        {
            if (fr.Bounds.Contains(worldPt))
            {
                _selectedState!.SelectedFrame = fr.Frame;
                return;
            }
        }
    }

    private void ApplyRegionToSelectedFrame(int minX, int minY, int maxX, int maxY)
    {
        if (_selectedState!.SelectedFrame is null || _bitmap is null) return;
        var frame = _selectedState!.SelectedFrame;
        float w = _bitmap.Width, h = _bitmap.Height;
        frame.LeftCoordinate   = minX / w;
        frame.RightCoordinate  = maxX / w;
        frame.TopCoordinate    = minY / h;
        frame.BottomCoordinate = maxY / h;
        RefreshFramesInternal();
        FrameRegionChanged?.Invoke(frame);
    }

    /// <summary>
    /// Applies the current hover-preview rect (<see cref="_previewRect"/>) to the selected frame.
    /// Used by Magic Wand double-click to commit the dashed-outline selection.
    /// No-op when no frame is selected, no bitmap is loaded, or no preview is active.
    /// </summary>
    private void ApplyPreviewToSelectedFrame()
    {
        if (!_showPreview || _selectedState!.SelectedFrame is null || _bitmap is null) return;
        ApplyRegionToSelectedFrame(
            (int)_previewRect.Left, (int)_previewRect.Top,
            (int)_previewRect.Right, (int)_previewRect.Bottom);
    }

    // ── Coordinate transforms ─────────────────────────────────────────────────

    private SKPoint ScreenToTexture(float sx, float sy)
    {
        var (tx, ty) = CanvasTransform.ScreenToTexture(sx, sy, _panX, _panY, _zoom);
        return new SKPoint(tx, ty);
    }

    private SKRect ToScreen(SKRect r)
    {
        var (l, t, rr, b) = CanvasTransform.TextureRectToScreen(
            r.Left, r.Top, r.Right, r.Bottom, _panX, _panY, _zoom);
        return new SKRect(l, t, rr, b);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshFramesInternal()
    {
        _frameRects.Clear();

        if (_bitmap is null) { InvalidateVisual(); return; }

        var selectedFrame  = _selectedState!.SelectedFrame;
        var selectedChain  = _selectedState!.SelectedChain;
        var selectedChains = _selectedState!.SelectedChains;

        string? achxFolder = string.IsNullOrEmpty(_projectManager!.FileName)
            ? null
            : (Path.GetDirectoryName(_projectManager!.FileName) ?? string.Empty);

        IEnumerable<AnimationFrameSave> framesToShow;
        if (selectedFrame != null)
            framesToShow = new[] { selectedFrame };
        else if (selectedChains?.Count > 0)
            framesToShow = selectedChains.SelectMany(c => c.Frames);
        else if (selectedChain?.Frames != null)
            framesToShow = selectedChain.Frames;
        else
            framesToShow = Array.Empty<AnimationFrameSave>();

        float w = _bitmap.Width;
        float h = _bitmap.Height;

        foreach (var frame in framesToShow)
        {
            if (string.IsNullOrEmpty(frame.TextureName)) continue;

            // Filter to frames that use the currently shown texture
            if (achxFolder != null && _loadedTexturePath != null)
            {
                var fp = new FilePath(Path.Combine(achxFolder, frame.TextureName));
                if (!fp.Equals(new FilePath(_loadedTexturePath))) continue;
            }

            float pixL = frame.LeftCoordinate   * w;
            float pixT = frame.TopCoordinate    * h;
            float pixR = frame.RightCoordinate  * w;
            float pixB = frame.BottomCoordinate * h;

            if (_showGrid && _gridSize > 0)
            {
                // Snap the display coordinates to grid line intersections for
                // visual feedback.  UV write-back is intentionally omitted —
                // grid is a future-edit setting and must never modify existing frames.
                static float Snap(float v, int g) => MathF.Round(v / g) * g;
                pixL = Snap(pixL, _gridSize);
                pixT = Snap(pixT, _gridSize);
                pixR = Snap(pixR, _gridSize);
                pixB = Snap(pixB, _gridSize);
                if (pixR <= pixL) pixR = pixL + _gridSize;
                if (pixB <= pixT) pixB = pixT + _gridSize;
            }

            _frameRects.Add(new FrameRect
            {
                Frame      = frame,
                Bounds     = new SKRect(pixL, pixT, pixR, pixB),
                IsSelected = frame == selectedFrame
            });
        }

        InvalidateVisual();
    }

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

    private string? DetermineTexturePath()
    {
        string? textureName = _selectedState!.SelectedFrame?.TextureName
                           ?? _selectedState!.SelectedChain?.Frames?.FirstOrDefault()?.TextureName;

        if (string.IsNullOrEmpty(textureName))
            return null;

        // If no ACHX is saved yet, the texture path is already absolute.
        if (string.IsNullOrEmpty(_projectManager!.FileName))
            return textureName;

        return Path.Combine(Path.GetDirectoryName(_projectManager!.FileName) ?? string.Empty, textureName);
    }

    /// <summary>
    /// Returns the pixel dimensions of the last frame in the currently selected
    /// animation chain, or (0, 0) if no chain/frames exist.  Used to determine
    /// the size of a new frame created by a plain-mode Ctrl+click.
    /// </summary>
    private (int w, int h) GetLastFramePixelSize()
    {
        if (_bitmap is null) return (0, 0);
        var chain = _selectedState!.SelectedChain;
        if (chain?.Frames == null || chain.Frames.Count == 0) return (0, 0);
        var last = chain.Frames[chain.Frames.Count - 1];
        int w = (int)Math.Round((last.RightCoordinate  - last.LeftCoordinate)  * _bitmap.Width);
        int h = (int)Math.Round((last.BottomCoordinate - last.TopCoordinate)   * _bitmap.Height);
        return (w, h);
    }

    /// <summary>
    /// Test-only: simulates a Ctrl+click at the given screen position in plain mode
    /// (no grid, no magic-wand).  No-op when the bitmap is null, the grid is active,
    /// or magic-wand mode is on.  Fires <see cref="FrameCreatedFromRegion"/> with the
    /// computed pixel bounds.
    /// </summary>
    public void SimulatePlainCtrlClick(float screenX, float screenY)
    {
        if (_bitmap is null || _showGrid || _isMagicWandMode) return;
        var world = ScreenToTexture(screenX, screenY);
        var (lastW, lastH) = GetLastFramePixelSize();
        var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(
            world.X, world.Y, _bitmap.Width, _bitmap.Height, lastW, lastH);
        FrameCreatedFromRegion?.Invoke(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Test-only: simulates a Magic Wand double-click at the given screen position.
    /// Updates the hover preview from the pixel at <paramref name="screenX"/>,
    /// <paramref name="screenY"/> and then applies <see cref="ApplyPreviewToSelectedFrame"/>,
    /// mirroring the double-click branch in <see cref="OnPointerPressed"/>.
    /// No-op when magic-wand mode is off, the bitmap is null, or there is no preview.
    /// </summary>
    public void SimulateWandDoubleClick(float screenX, float screenY)
    {
        if (!_isMagicWandMode || _bitmap is null) return;
        UpdatePreview(new Point(screenX, screenY));
        if (_showPreview)
            ApplyPreviewToSelectedFrame();
    }

    /// <summary>
    /// Builds a cross-hair "+" cursor and returns a <see cref="Cursor"/>
    /// with its hot-spot at the centre pixel.  Called once by <see cref="_addFrameCursorLazy"/>.
    /// </summary>
    private static Cursor CreateAddFrameCursor() =>
        new Cursor(StandardCursorType.Cross);
}
