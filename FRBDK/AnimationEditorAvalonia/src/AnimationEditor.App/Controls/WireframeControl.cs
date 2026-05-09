using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using FlatRedBall.Content.AnimationChain;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FilePath = FlatRedBall.IO.FilePath;

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

        public DrawOp(RenderSnapshot s) { _s = s; Bounds = new Rect(0, 0, s.Width, s.Height); }

        public Rect Bounds { get; }
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext ctx)
        {
            var lease = ctx.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
            if (lease is null) return;
            using (lease)
                RenderSk(lease.SkCanvas, _s);
        }

        // ── Static rendering logic ────────────────────────────────────────────

        internal static void RenderSk(SKCanvas canvas, RenderSnapshot s)
        {
            canvas.Clear(new SKColor(30, 30, 30));

            if (s.Image != null)
            {
                var dest = new SKRect(
                    s.PanX, s.PanY,
                    s.PanX + s.ImageWidth * s.Zoom,
                    s.PanY + s.ImageHeight * s.Zoom);

                // Texture image — point sampling when zoomed ≥ 1× for pixel-art fidelity
                using var imgPaint = new SKPaint();
                imgPaint.FilterQuality = s.Zoom >= 1f ? SKFilterQuality.None : SKFilterQuality.Low;
                canvas.DrawImage(s.Image, dest, imgPaint);

                // Outline around whole texture
                using var outlinePaint = new SKPaint
                {
                    Color = new SKColor(255, 255, 255, 160),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1f
                };
                canvas.DrawRect(dest, outlinePaint);

                // Grid overlay
                if (s.ShowGrid && s.GridSize > 0)
                    DrawGrid(canvas, s, dest);
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

        private static void DrawGrid(SKCanvas canvas, RenderSnapshot s, SKRect textureDest)
        {
            using var paint = new SKPaint
            {
                Color        = new SKColor(255, 255, 255, 35),
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

        private static void DrawHandles(SKCanvas canvas, SKRect sr)
        {
            const float Hs = 5f;
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
            yield return new SKPoint(r.Left, r.Top);
            yield return new SKPoint(cx, r.Top);
            yield return new SKPoint(r.Right, r.Top);
            yield return new SKPoint(r.Left, cy);
            yield return new SKPoint(r.Right, cy);
            yield return new SKPoint(r.Left, r.Bottom);
            yield return new SKPoint(cx, r.Bottom);
            yield return new SKPoint(r.Right, r.Bottom);
        }

        private static SKRect ToScreen(SKRect r, RenderSnapshot s)
        {
            var (l, t, rr, b) = WireframeTransform.TextureRectToScreen(
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
    private float _panX, _panY;

    // Minimum dead-space padding (pixels) around the image so the user can pan
    // into the empty area on every side.  Always applied at all zoom levels.
    private const float PanPadding = 300f;

    // Extra scrollable room (pixels) beyond the centering margin so that the
    // ScrollViewer always has a non-zero scroll range even for tiny images that
    // fit well inside the viewport.  This ensures the scrollbar buttons (+/−)
    // are always active regardless of zoom.
    private const float ExtraScrollable = 50f;

    private bool _showGrid;
    private int _gridSize = 16;

    private readonly List<FrameRect> _frameRects = new();

    // ScrollViewer integration (optional — wired up by MainWindow)
    private ScrollViewer? _scrollViewer;
    private float _scrollAnchorX, _scrollAnchorY;
    // Synchronously-maintained mirror of the intended scroll target.  ZoomToward
    // writes this before queuing the deferred scroll so rapid wheel events each
    // chain off the correct accumulated target rather than the still-stale visual
    // scroll offset.
    private float _scrollTargetX, _scrollTargetY;
    // Post-layout pending scroll: the offset we intend to apply once the layout
    // pass after a ZoomToward has completed.  Separate from _scrollTargetX/Y so
    // that the intermediate ScrollChanged(Offset=0) fired when content grows does
    // not overwrite the intended value before we have a chance to apply it.
    private float _pendingScrollX, _pendingScrollY;
    private bool _pendingScrollApply;
    // Incremented whenever a new pending scroll is queued or cancelled, so that
    // stale Background-priority dispatcher callbacks can detect they are outdated.
    private int _pendingScrollGeneration;

    // ── Debug tooling (toggle with F2 in the live app) ────────────────────────
    private bool _debugMode;
    private int  _dbgPanMoveCount;
    private static readonly string _debugLogPath =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wireframe_debug.log");
    private static readonly Typeface _dbgTypeface = new("Consolas, Courier New");
    private static readonly ISolidColorBrush _dbgBg = new SolidColorBrush(Color.FromArgb(210, 0, 0, 0));
    private static readonly ISolidColorBrush _dbgFg = new SolidColorBrush(Color.FromRgb(0, 255, 80));

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
        var vp   = _scrollViewer?.Viewport ?? new Size(Bounds.Width, Bounds.Height);
        var off  = _scrollViewer?.Offset   ?? new Vector(0, 0);
        var ext  = _scrollViewer?.Extent   ?? new Size(0, 0);
        int bmpW = _bitmap?.Width  ?? 0;
        int bmpH = _bitmap?.Height ?? 0;

        var lines = new[]
        {
            "── WIREFRAME DEBUG (F2 to hide) ──",
            $"zoom          {_zoom * 100f,7:F1}%",
            $"sv.Offset     X={off.X,7:F1}  Y={off.Y:F1}",
            $"sv.Extent     W={ext.Width,6:F0}  H={ext.Height:F0}",
            $"scrollTarget  X={_scrollTargetX,7:F1}  Y={_scrollTargetY:F1}",
            $"pendingApply  {(_pendingScrollApply ? "TRUE  ←" : "false")}",
            $"pendingXY     X={_pendingScrollX,7:F1}  Y={_pendingScrollY:F1}",
            $"panXY         X={_panX,7:F1}  Y={_panY:F1}",
            $"viewport      {vp.Width:F0} × {vp.Height:F0}",
            $"content       {bmpW * _zoom:F0} × {bmpH * _zoom:F0}",
            $"useScrollPan  {UseScrollPan()}",
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

    // Preview rectangle (magic wand / grid snap hover)
    private bool _showPreview;
    private SKRect _previewRect;

    // Per-texture saved camera (texture path → panX, panY, zoom, scrollTargetX, scrollTargetY).
    // In scroll-pan mode px/py are always epX/epY at save time; sx/sy are the scroll offsets.
    private readonly Dictionary<string, (float px, float py, float z, float sx, float sy)> _cameraByTexture = new();

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
    /// Fired on every pointer move while dragging a handle (live update).
    /// Does NOT trigger save or tree refresh — use <see cref="FrameRegionChanged"/> for those.
    /// </summary>
    public event Action<AnimationFrameSave>? FrameLiveUpdated;

    /// <summary>
    /// Fired after every zoom change. Payload is the new zoom as a percentage (e.g. 100f = 100 %).
    /// </summary>
    public event Action<float>? ZoomChanged;

    /// <summary>
    /// Fired when the user ctrl+clicks to add a new frame
    /// (minX, minY, maxX, maxY in texture pixel coords).
    /// </summary>
    public event Action<int, int, int, int>? FrameCreatedFromRegion;

    // ── Constructor ───────────────────────────────────────────────────────────

    public WireframeControl()
    {
        ClipToBounds = true;
        Focusable = true;

        // Wire into Core event system
        SelectedState.Self.SelectionChanged += () => Dispatcher.UIThread.InvokeAsync(RefreshAll);
        AppCommands.Self.RefreshWireframeRequested += () => Dispatcher.UIThread.InvokeAsync(RefreshAll);
        ApplicationEvents.Self.AchxLoaded += _ => Dispatcher.UIThread.InvokeAsync(RefreshAll);

        // Safety-net: LayoutUpdated fires after the FULL layout pass, at which point
        // sv.Extent is guaranteed to reflect the new (larger) content size.  If the
        // ScrollChanged(ExtentDelta) path tried to apply the pending scroll but the
        // extent wasn't ready yet, this catches it.
        LayoutUpdated += (_, _) =>
        {
            DebugLog("LAYOUT_TICK",
                $"pending={_pendingScrollApply} " +
                $"offset=({_scrollViewer?.Offset.X:F1},{_scrollViewer?.Offset.Y:F1}) " +
                $"extent=({_scrollViewer?.Extent.Width:F1},{_scrollViewer?.Extent.Height:F1}) " +
                $"target=({_scrollTargetX:F1},{_scrollTargetY:F1})");
            if (_pendingScrollApply && _scrollViewer != null)
                TryApplyPendingScroll("LAYOUT_UPDATED");
        };
    }

    // ── ScrollViewer integration ──────────────────────────────────────────────

    /// <summary>
    /// Attaches a ScrollViewer so that large textures get horizontal and vertical scrollbars.
    /// Call once from the hosting window after InitializeComponent.
    /// </summary>
    public void AttachScrollViewer(ScrollViewer sv)
    {
        _scrollViewer = sv;
        // Keep _scrollTargetX/Y in sync when an external source (e.g. user dragging
        // a scrollbar) changes the offset so subsequent ZoomToward calls use the
        // correct base.
        sv.ScrollChanged += (_, e) =>
        {
            if (_scrollViewer == null) return;

            // When the content extent grows, attempt to apply the pending scroll now.
            // TryApplyPendingScroll guards on the extent being large enough; if not ready
            // it returns without clearing _pendingScrollApply so LayoutUpdated retries.
            if (_pendingScrollApply && (e.ExtentDelta.X != 0 || e.ExtentDelta.Y != 0))
            {
                DebugLog("SCROLL_CHG",
                    $"[EXTENT_CHANGED] extentDelta=({e.ExtentDelta.X:F1},{e.ExtentDelta.Y:F1}) " +
                    $"newExtent=({_scrollViewer.Extent.Width:F1},{_scrollViewer.Extent.Height:F1}) " +
                    $"maxScrollX={Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width):F1} " +
                    $"pendingX={_pendingScrollX:F1}");
                TryApplyPendingScroll("SCROLL_CHG_EXTENT");
                return;
            }

            // Normal path: mirror sv.Offset into _scrollTargetX so ZoomToward chains correctly.
            // SKIP when a scroll is still pending: _scrollTargetX already holds the intended
            // target, and sv.Offset.X may be stale/clamped (0) before the pending apply fires.
            if (_pendingScrollApply)
            {
                DebugLog("SCROLL_CHG",
                    $"[SKIPPED – pending] offset=({_scrollViewer.Offset.X:F1},{_scrollViewer.Offset.Y:F1}) " +
                    $"keeping _scrollTargetX={_scrollTargetX:F1}");
                return;
            }

            float prevX = _scrollTargetX;
            _scrollTargetX = (float)_scrollViewer.Offset.X;
            _scrollTargetY = (float)_scrollViewer.Offset.Y;
            // Warn when the normal path silently resets a non-zero scroll target
            // to near-zero (the primary signal for diagnosing pan-lock).
            if (prevX > 5f && _scrollTargetX < 1f)
                DebugLog("SCROLL_CHG",
                    $"⚠ ZERO_RESET prevTargetX={prevX:F1} → {_scrollTargetX:F1} " +
                    $"sv.Offset.X={_scrollViewer.Offset.X:F1} " +
                    $"extent={_scrollViewer.Extent.Width:F1} vp={_scrollViewer.Viewport.Width:F1}");
            DebugLog("SCROLL_CHG",
                $"offset=({_scrollViewer.Offset.X:F1},{_scrollViewer.Offset.Y:F1}) " +
                $"prevTargetX={prevX:F1} → newTargetX={_scrollTargetX:F1}");
        };
    }

    /// <summary>
    /// Records the intended scroll offset and flags that it should be applied once the
    /// layout pass after a ZoomToward has completed.  Primary application is done inside
    /// the <c>ScrollChanged(ExtentDelta)</c> and <c>LayoutUpdated</c> handlers.
    ///
    /// A secondary safety net is queued at <c>DispatcherPriority.Background</c> (4).
    /// Background priority is lower than Render (7) where layout/arrange runs, so this
    /// dispatcher item is guaranteed to fire AFTER the layout pass that commits the new
    /// <c>sv.Extent</c>.  If the primary handlers already applied the scroll, this is
    /// a no-op (generation mismatch or <c>_pendingScrollApply == false</c>).
    /// </summary>
    private void QueueScrollAfterLayout(float x, float y)
    {
        DebugLog("QUEUE_SCROLL",
            $"x={x:F1} y={y:F1} | prevTargetX={_scrollTargetX:F1} " +
            $"prevPending=({_pendingScrollX:F1},{_pendingScrollY:F1}) prevApply={_pendingScrollApply}");
        _scrollTargetX = x;
        _scrollTargetY = y;
        _pendingScrollX = x;
        _pendingScrollY = y;
        _pendingScrollApply = true;

        // Background dispatcher safety net — fires after layout (DispatcherPriority.Background=4
        // < Render=7, so it is scheduled AFTER all layout/arrange work).
        // Generation number ensures only the most recent queue call actually applies.
        int gen = ++_pendingScrollGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (gen != _pendingScrollGeneration || !_pendingScrollApply || _scrollViewer == null)
            {
                DebugLog("DISPATCH_BG",
                    $"skip: gen={gen} curGen={_pendingScrollGeneration} " +
                    $"apply={_pendingScrollApply}");
                return;
            }
            DebugLog("DISPATCH_BG",
                $"enter: offset=({_scrollViewer.Offset.X:F1},{_scrollViewer.Offset.Y:F1}) " +
                $"extent=({_scrollViewer.Extent.Width:F1},{_scrollViewer.Extent.Height:F1}) " +
                $"pendingX={_pendingScrollX:F1}");
            TryApplyPendingScroll("DISPATCHER_BG");
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Cancels any queued scroll-after-layout and optionally sets new target values.
    /// Call before any direct offset write (e.g. CenterTexture) to prevent a stale
    /// pending zoom-scroll from overriding the new value.
    /// </summary>
    private void CancelPendingScrollApply(float? x = null, float? y = null)
    {
        _pendingScrollApply = false;
        _pendingScrollGeneration++; // invalidate any queued Background dispatcher item
        if (x.HasValue) { _pendingScrollX = x.Value; _scrollTargetX = x.Value; }
        if (y.HasValue) { _pendingScrollY = y.Value; _scrollTargetY = y.Value; }
    }

    /// <summary>
    /// Tries to apply the pending scroll offset.  Guards on <c>sv.Extent</c> having
    /// grown to accommodate <c>_pendingScrollX/Y</c>.  If the extent hasn't been
    /// updated yet (layout pass still in progress), returns without clearing
    /// <c>_pendingScrollApply</c> so the <c>LayoutUpdated</c> handler can retry.
    /// Legitimate over-shot targets (scroll > maxScroll at the current zoom) are
    /// clamped to the actual maximum rather than deferred.
    /// </summary>
    private void TryApplyPendingScroll(string caller)
    {
        if (!_pendingScrollApply || _scrollViewer == null) return;

        // Guard: is the extent large enough to hold the intended offset?
        // We need Extent.Width >= _pendingScrollX + Viewport.Width.
        // If it isn't, the layout pass that grows the content hasn't completed yet —
        // defer rather than let Avalonia clamp the offset to 0 (which would reset
        // _scrollTargetX to 0 and cause pan-lock).
        if (_pendingScrollX > 0)
        {
            double minExtentW = _pendingScrollX + _scrollViewer.Viewport.Width;
            if (_scrollViewer.Extent.Width < minExtentW - 1f)
            {
                DebugLog("APPLY_SCROLL",
                    $"[{caller}] NOT READY (X): need extentW≥{minExtentW:F1} " +
                    $"actual={_scrollViewer.Extent.Width:F1} – deferring");
                return;
            }
        }
        if (_pendingScrollY > 0)
        {
            double minExtentH = _pendingScrollY + _scrollViewer.Viewport.Height;
            if (_scrollViewer.Extent.Height < minExtentH - 1f)
            {
                DebugLog("APPLY_SCROLL",
                    $"[{caller}] NOT READY (Y): need extentH≥{minExtentH:F1} " +
                    $"actual={_scrollViewer.Extent.Height:F1} – deferring");
                return;
            }
        }

        // Clamp to actual max scroll (handles legitimate over-shot when the zoom
        // pivot is near the far edge of the image).
        float maxScrollX = (float)Math.Max(0, _scrollViewer.Extent.Width  - _scrollViewer.Viewport.Width);
        float maxScrollY = (float)Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        float applyX = Math.Min(_pendingScrollX, maxScrollX);
        float applyY = Math.Min(_pendingScrollY, maxScrollY);

        _pendingScrollApply = false;
        // Pre-synchronise _scrollTargetX/Y to the value we are about to apply so that
        // any ScrollChanged(OffsetDelta) that fires synchronously from within the Offset
        // assignment below does NOT overwrite them with a stale/clamped value.
        _scrollTargetX = applyX;
        _scrollTargetY = applyY;
        _scrollViewer.Offset = new Vector(applyX, applyY);

        float actualX = (float)_scrollViewer.Offset.X;
        float actualY = (float)_scrollViewer.Offset.Y;
        DebugLog("APPLY_SCROLL",
            $"[{caller}] applied=({applyX:F1},{applyY:F1}) actual=({actualX:F1},{actualY:F1}) " +
            $"extent=({_scrollViewer.Extent.Width:F1},{_scrollViewer.Extent.Height:F1})");

        // Safety net: if the offset was still clamped despite the extent guard (e.g.
        // Avalonia deferred the Extent update until after the ScrollChanged event),
        // re-queue so LayoutUpdated retries with the fully-committed extent.
        if (Math.Abs(actualX - applyX) > 2f || Math.Abs(actualY - applyY) > 2f)
        {
            DebugLog("APPLY_SCROLL",
                $"[{caller}] CLAMPED despite guard – re-queuing. actualX={actualX:F1} expected={applyX:F1}");
            _pendingScrollApply = true;
            _scrollTargetX = _pendingScrollX;
            _scrollTargetY = _pendingScrollY;
            // Re-queue a fresh Background dispatcher safety net for the retry.
            int gen = ++_pendingScrollGeneration;
            Dispatcher.UIThread.Post(() =>
            {
                if (gen != _pendingScrollGeneration || !_pendingScrollApply || _scrollViewer == null) return;
                DebugLog("DISPATCH_BG",
                    $"RETRY enter: offset=({_scrollViewer.Offset.X:F1},{_scrollViewer.Offset.Y:F1}) " +
                    $"extent=({_scrollViewer.Extent.Width:F1},{_scrollViewer.Extent.Height:F1}) " +
                    $"pendingX={_pendingScrollX:F1}");
                TryApplyPendingScroll("DISPATCHER_BG_RETRY");
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Returns the effective padding (pixels) to add on each side of the image in the X
    /// direction.  Always at least <see cref="PanPadding"/>; grows dynamically for images
    /// smaller than the viewport so that the content is always wider than the viewport
    /// by at least <see cref="ExtraScrollable"/> pixels — keeping the scrollbar active.
    /// </summary>
    private float EffectivePaddingX()
    {
        if (_scrollViewer == null || _bitmap == null) return PanPadding;
        float halfFree = ((float)_scrollViewer.Viewport.Width - _bitmap.Width * _zoom) / 2f;
        return Math.Max(PanPadding, halfFree + ExtraScrollable);
    }

    /// <summary>
    /// Returns the effective padding (pixels) to add on each side of the image in the Y
    /// direction.  See <see cref="EffectivePaddingX"/> for the rationale.
    /// </summary>
    private float EffectivePaddingY()
    {
        if (_scrollViewer == null || _bitmap == null) return PanPadding;
        float halfFree = ((float)_scrollViewer.Viewport.Height - _bitmap.Height * _zoom) / 2f;
        return Math.Max(PanPadding, halfFree + ExtraScrollable);
    }

    /// <summary>Returns true when the ScrollViewer is attached and has a valid viewport,
    /// so the control is in scroll mode.  Scroll mode is always active when a ScrollViewer
    /// is present — padding is added at every zoom level so the scrollbars are always
    /// usable (the user can always pan to see the entity-origin crosshair).</summary>
    private bool OverflowX()
    {
        if (_scrollViewer == null || _bitmap == null) return false;
        return _scrollViewer.Viewport.Width > 1;
    }

    /// <summary>Returns true when the ScrollViewer is attached and has a valid viewport.
    /// See <see cref="OverflowX"/>.</summary>
    private bool OverflowY()
    {
        if (_scrollViewer == null || _bitmap == null) return false;
        return _scrollViewer.Viewport.Height > 1;
    }

    /// <summary>Returns true when the control is hosted in a ScrollViewer with a valid
    /// viewport — the image is always in scroll-pan mode when a ScrollViewer is present.</summary>
    private bool UseScrollPan() => OverflowX() || OverflowY();

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_bitmap == null)
            return new Size(0, 0);

        double imageW = _bitmap.Width  * _zoom;
        double imageH = _bitmap.Height * _zoom;

        // When hosted in a ScrollViewer, always add effective padding on every side so
        // the user can pan beyond the image boundary at any zoom level.  The padding
        // grows dynamically for small images to ensure the content is always wider than
        // the viewport, keeping the scrollbar buttons active.
        if (_scrollViewer != null && _scrollViewer.Viewport.Width > 1)
        {
            float epX = EffectivePaddingX();
            float epY = EffectivePaddingY();
            var size = new Size(imageW + 2 * epX, imageH + 2 * epY);

            // If a scroll-after-layout is pending, do nothing here — the scroll will be
            // applied from the ScrollChanged handler when ExtentDelta != 0 fires, at which
            // point sv.Extent already reflects the new size and the offset won't be clamped.
            if (_pendingScrollApply)
            {
                DebugLog("MEASURE_PENDING",
                    $"pendingX={_pendingScrollX:F1} " +
                    $"content=({imageW:F0},{imageH:F0}) viewport=({_scrollViewer.Viewport.Width:F1},{_scrollViewer.Viewport.Height:F1}) " +
                    $"epX={epX:F1} epY={epY:F1} — waiting for ScrollChanged(ExtentDelta)");
            }

            return size;
        }

        double contentW = double.IsFinite(availableSize.Width)
            ? Math.Max(availableSize.Width,  imageW)
            : imageW;
        double contentH = double.IsFinite(availableSize.Height)
            ? Math.Max(availableSize.Height, imageH)
            : imageH;

        return new Size(contentW, contentH);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Load a PNG from disk and show it. Pass null to clear the view.
    /// Saves the camera position for the old texture and restores it for the new one.
    /// </summary>
    public void LoadTexture(string? filePath)
    {
        // Normalise path for comparison
        string? norm = string.IsNullOrEmpty(filePath) ? null : new FilePath(filePath).Standardized;

        if (_loadedTexturePath == norm)
        {
            // Texture hasn't changed, but the selected frame may have, so update frame rects.
            RefreshFramesInternal();
            return;
        }

        // Save camera for the texture we're leaving
        if (_loadedTexturePath != null)
            _cameraByTexture[_loadedTexturePath] = (_panX, _panY, _zoom, _scrollTargetX, _scrollTargetY);

        _loadedTexturePath = norm;
        _image?.Dispose();
        _image = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _inspectableImage = null;

        if (norm != null && File.Exists(norm))
        {
            _bitmap = SKBitmap.Decode(norm);
            // Upload pixels into an immutable SKImage on the UI thread so the
            // render thread never touches the SKBitmap directly. Without this,
            // SKCanvas.DrawBitmap on the render thread crashes with AV.
            _image = SKImage.FromBitmap(_bitmap);

            if (_isMagicWandMode)
                _inspectableImage = new InspectableImage(_bitmap);

            if (_cameraByTexture.TryGetValue(norm, out var cam))
            {
                _zoom = cam.z;
                if (_scrollViewer != null)
                {
                    // In scroll-pan mode panX/Y must always equal EffectivePaddingX/Y.
                    // CenterTexture() sets panX = epX and queues a centred scroll; we then
                    // override that pending scroll with the saved target if one exists.
                    CenterTexture();
                    if (cam.sx != 0f || cam.sy != 0f)
                    {
                        CancelPendingScrollApply(x: cam.sx, y: cam.sy);
                        QueueScrollAfterLayout(cam.sx, cam.sy);
                    }
                }
                else
                {
                    (_panX, _panY) = (cam.px, cam.py);
                    InvalidateMeasure();
                }
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

    /// <summary>Re-detect the current texture from the selection, reload it, and refresh frames.</summary>
    public void RefreshAll()
    {
        var path = DetermineTexturePath();
        LoadTexture(path);
    }

    /// <summary>Set zoom by whole-number percentage (e.g. 100 = 1× fit).</summary>
    public void SetZoomPercent(int percent)
    {
        float newZoom = Math.Clamp(percent / 100f, WireframeTransform.MinZoom, WireframeTransform.MaxZoom);
        float cx, cy;
        if (_scrollViewer != null && _scrollViewer.Viewport.Width > 1)
        {
            // Zoom toward the center of the visible viewport
            cx = _scrollTargetX + (float)(_scrollViewer.Viewport.Width  / 2);
            cy = _scrollTargetY + (float)(_scrollViewer.Viewport.Height / 2);
        }
        else
        {
            cx = (float)Bounds.Width  / 2;
            cy = (float)Bounds.Height / 2;
        }
        ZoomToward(cx, cy, newZoom / _zoom);
    }

    /// <summary>Toggle the grid overlay and update the grid cell size.</summary>
    public void SetGrid(bool show, int cellSize)
    {
        _showGrid = show;
        _gridSize = cellSize;
        // Snap and write UV back when grid is enabled so existing frames immediately
        // align to grid lines.  This is the only place that mutates frame UV data;
        // passive RefreshFramesInternal calls (e.g. from SelectionChanged) do not.
        ApplyGridSnap();
        RefreshFramesInternal();
    }

    /// <summary>
    /// Snaps the UV coordinates of every currently-visible frame to the active
    /// grid and writes them back to the <see cref="AnimationFrameSave"/> objects.
    /// Called only from <see cref="SetGrid"/> so that passive refreshes triggered
    /// by selection changes cannot silently corrupt animation data.
    /// </summary>
    private void ApplyGridSnap()
    {
        if (!_showGrid || _gridSize <= 0 || _bitmap is null) return;

        var selectedFrame  = SelectedState.Self.SelectedFrame;
        var selectedChain  = SelectedState.Self.SelectedChain;
        var selectedChains = SelectedState.Self.SelectedChains;

        string? achxFolder = string.IsNullOrEmpty(ProjectManager.Self.FileName)
            ? null
            : FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName);

        IEnumerable<AnimationFrameSave> framesToSnap;
        if (selectedFrame != null)
            framesToSnap = new[] { selectedFrame };
        else if (selectedChains?.Count > 0)
            framesToSnap = selectedChains.SelectMany(c => c.Frames);
        else if (selectedChain?.Frames != null)
            framesToSnap = selectedChain.Frames;
        else
            return;

        float w = _bitmap.Width;
        float h = _bitmap.Height;
        static float Snap(float v, int g) => MathF.Round(v / g) * g;

        foreach (var frame in framesToSnap)
        {
            if (string.IsNullOrEmpty(frame.TextureName)) continue;

            if (achxFolder != null && _loadedTexturePath != null)
            {
                var fp = new FilePath(achxFolder + frame.TextureName);
                if (!fp.Equals(new FilePath(_loadedTexturePath))) continue;
            }

            float pixL = Snap(frame.LeftCoordinate   * w, _gridSize);
            float pixT = Snap(frame.TopCoordinate    * h, _gridSize);
            float pixR = Snap(frame.RightCoordinate  * w, _gridSize);
            float pixB = Snap(frame.BottomCoordinate * h, _gridSize);
            if (pixR <= pixL) pixR = pixL + _gridSize;
            if (pixB <= pixT) pixB = pixT + _gridSize;

            frame.LeftCoordinate   = pixL / w;
            frame.RightCoordinate  = pixR / w;
            frame.TopCoordinate    = pixT / h;
            frame.BottomCoordinate = pixB / h;
        }
    }

    /// <summary>
    /// Directly sets the camera state (pan and zoom) without centering logic.
    /// Useful for tests that need a predictable, axis-aligned view.
    /// </summary>
    public void SetCamera(float panX, float panY, float zoom)
    {
        _panX = panX;
        _panY = panY;
        _zoom = zoom;
        InvalidateVisual();
    }

    /// <summary>Current grid show/size state. For tests.</summary>
    public (bool ShowGrid, int GridSize) GridState => (_showGrid, _gridSize);

    /// <summary>Raw camera state (panX, panY, zoom). For tests.
    /// In scroll mode panX/panY equal PanPadding (> 0); the visible region
    /// is determined by the ScrollViewer offset.</summary>
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

        ApplyHandleDrag(new Point(endScreenX, endScreenY));

        FrameRegionChanged?.Invoke(sel.Frame);
        _draggingRect   = null;
        _draggingHandle = HandleKind.None;
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
        if (UseScrollPan())
        {
            bool ovX = OverflowX();
            bool ovY = OverflowY();
            float dX = vpX - (float)_panAnchor.X;
            float dY = vpY - (float)_panAnchor.Y;

            float newScrollX = _scrollTargetX;
            float newScrollY = _scrollTargetY;

            if (ovX)
            {
                newScrollX = (float)Math.Max(0, _scrollAnchorX - dX);
                _scrollTargetX = newScrollX;
            }
            else
            {
                _panX = _panAnchorX + dX;
                newScrollX = 0f;
            }

            if (ovY)
            {
                newScrollY = (float)Math.Max(0, _scrollAnchorY - dY);
                _scrollTargetY = newScrollY;
            }
            else
            {
                _panY = _panAnchorY + dY;
                newScrollY = 0f;
            }

            _scrollViewer!.Offset = new Vector(newScrollX, newScrollY);
        }
        else
        {
            (_panX, _panY) = WireframeTransform.Pan(
                _panAnchorX, _panAnchorY,
                (float)_panAnchor.X, (float)_panAnchor.Y,
                vpX, vpY);
            InvalidateVisual();
        }
    }

    /// <summary>Test-only: ends the pan gesture started by <see cref="SimulatePanStart"/>.</summary>
    public void SimulatePanEnd() => _isPanning = false;

    /// <summary>
    /// Test-only: simulates a single mouse-wheel zoom event toward the given
    /// <b>viewport-space</b> point.  Mirrors <see cref="OnPointerWheelChanged"/>
    /// exactly: converts the viewport-space pivot to content-space by adding
    /// <c>_scrollTargetX/Y</c>, then calls <see cref="ZoomToward"/>.
    /// <para><paramref name="factor"/> is the zoom scale factor (e.g. 1.25 to
    /// zoom in by one wheel notch, 1/1.25 to zoom out).</para>
    /// </summary>
    public void SimulateWheelZoom(float vpX, float vpY, float factor)
    {
        float scrollOffX = _scrollViewer is not null ? _scrollTargetX : 0f;
        float scrollOffY = _scrollViewer is not null ? _scrollTargetY : 0f;
        ZoomToward(vpX + scrollOffX, vpY + scrollOffY, factor);
    }

    /// <summary>Exposed for tests: the synchronous scroll-target maintained by ZoomToward.</summary>
    public (float X, float Y) ScrollTarget => (_scrollTargetX, _scrollTargetY);

    /// <summary>Test-only: current free-pan offset (used when overflowX or overflowY is false).</summary>
    public (float X, float Y) PanOffset => (_panX, _panY);

    /// <summary>Test-only: whether a pending post-layout scroll is queued.</summary>
    public bool PendingScrollApply => _pendingScrollApply;

    /// <summary>Test-only: the pending scroll target values queued by QueueScrollAfterLayout.</summary>
    public (float X, float Y) PendingScrollTarget => (_pendingScrollX, _pendingScrollY);

    // ── Rendering ─────────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        var snap = BuildSnapshot(Bounds.Width, Bounds.Height);
        ctx.Custom(new DrawOp(snap));
        DrawDebugOverlay(ctx);
    }

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
        var snap   = BuildSnapshot(width, height);
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        DrawOp.RenderSk(canvas, snap);
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
        (_panX, _panY, _zoom) = WireframeTransform.CenterFit(
            _bitmap.Width, _bitmap.Height, width, height);
        InvalidateVisual();
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
        if (sel != null)
        {
            snap.SelectedHandleBounds = sel.Bounds;

            // Compute the entity-origin position in texture space.
            // frame.Bounds are already in texture pixels; the entity's (0,0) is offset
            // from the frame's center by (-RelativeX, +RelativeY) in game space
            // (RelativeX/Y stored in display pixels = stored * OffsetMultiplier).
            // Game Y+ = screen up = texture row decreasing → negate RelativeY.
            float offMult = AppState.Self.OffsetMultiplier;
            snap.OriginTexX = sel.Bounds.MidX - sel.Frame.RelativeX * offMult;
            snap.OriginTexY = sel.Bounds.MidY + sel.Frame.RelativeY * offMult;
        }

        return snap;
    }

    // ── Mouse input ───────────────────────────────────────────────────────────

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        float factor = e.Delta.Y > 0 ? 1.25f : 1f / 1.25f;
        // e.GetPosition(this) returns content-space coords inside a ScrollViewer
        // (viewport position + scroll offset).  Using the ScrollViewer as the reference
        // gives stable viewport-space coords that are independent of the current offset.
        // Add the scroll offset to convert viewport-space → content-space for ZoomToward.
        var pivotVp = _scrollViewer != null ? e.GetPosition(_scrollViewer) : e.GetPosition(this);
        float scrollOffX = _scrollViewer is not null ? _scrollTargetX : 0f;
        float scrollOffY = _scrollViewer is not null ? _scrollTargetY : 0f;
        ZoomToward((float)pivotVp.X + scrollOffX, (float)pivotVp.Y + scrollOffY, factor);
        e.Handled = true;
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
            // In scroll-pan mode use the ScrollViewer as the GetPosition reference so
            // that the anchor is in viewport-space (no scroll offset baked in).
            // Using GetPosition(this) in scroll mode would give content-space coords
            // and create a feedback loop that makes the scroll oscillate.
            var panAnchor = _scrollViewer != null ? e.GetPosition(_scrollViewer) : pos;
            StartPan(panAnchor);
            e.Pointer.Capture(this);
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        // 1. Hit-test resize handles on the selected frame
        if (!isCtrl)
        {
            var (hitFrame, hitHandle) = HitTestHandle(pos);
            if (hitHandle != HandleKind.None)
            {
                _draggingRect = hitFrame;
                _draggingHandle = hitHandle;
                _dragStartWorld = ScreenToTexture((float)pos.X, (float)pos.Y);
                _dragStartBounds = hitFrame!.Bounds;
                e.Pointer.Capture(this);
                return;
            }
        }

        if (_bitmap is null) return;

        var world = ScreenToTexture((float)pos.X, (float)pos.Y);

        // 2. Magic-wand mode
        if (_isMagicWandMode && _inspectableImage != null)
        {
            _inspectableImage.GetOpaqueWandBounds(
                (int)world.X, (int)world.Y,
                out int minX, out int minY, out int maxX, out int maxY);

            if (maxX >= minX && maxY >= minY)
            {
                if (isCtrl)
                    FrameCreatedFromRegion?.Invoke(minX, minY, maxX, maxY);
                else
                    ApplyRegionToSelectedFrame(minX, minY, maxX, maxY);
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

        // 4. Click on an unselected frame to select it
        TrySelectFrameAtPoint(world);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_isPanning)
        {
            if (UseScrollPan())
            {
                bool ovX = OverflowX();
                bool ovY = OverflowY();
                // Use the ScrollViewer as the position reference (viewport-space) so the
                // pan delta is a pure physical displacement independent of scroll offset.
                // GetPosition(this) is content-space (viewport + scroll), which creates a
                // feedback loop: changing the offset shifts content-space pos even when the
                // mouse hasn't moved, causing scroll to oscillate every frame.
                var vpPos = e.GetPosition(_scrollViewer!);
                float dX = (float)(vpPos.X - _panAnchor.X);
                float dY = (float)(vpPos.Y - _panAnchor.Y);

                float newScrollX = _scrollTargetX;
                float newScrollY = _scrollTargetY;
                bool needsRedraw = false;

                // Per-axis: overflowing axes use scroll offset; non-overflowing use free-pan (_panX/Y).
                if (ovX)
                {
                    newScrollX = (float)Math.Max(0, _scrollAnchorX - dX);
                    _scrollTargetX = newScrollX;
                }
                else
                {
                    _panX = _panAnchorX + dX;
                    newScrollX = 0f;
                    needsRedraw = true;
                }

                if (ovY)
                {
                    newScrollY = (float)Math.Max(0, _scrollAnchorY - dY);
                    _scrollTargetY = newScrollY;
                }
                else
                {
                    _panY = _panAnchorY + dY;
                    newScrollY = 0f;
                    needsRedraw = true;
                }

                // Keep _scrollTargetX/Y in sync so subsequent ZoomToward or StartPan
                // calls use the correct offset even before the compositor renders.
                _scrollViewer!.Offset = new Vector(newScrollX, newScrollY);
                if (needsRedraw) InvalidateVisual();
                if (_dbgPanMoveCount++ < 20)
                    DebugLog("PAN_MOVE",
                        $"#{_dbgPanMoveCount} ovX={ovX} ovY={ovY} " +
                        $"anchor=({_scrollAnchorX:F1},{_scrollAnchorY:F1}) " +
                        $"vpPosX={vpPos.X:F1} dX={dX:F1} → scrollX={newScrollX:F1} panX={_panX:F1} " +
                        $"actual={_scrollViewer.Offset.X:F1}");
                // Do NOT call InvalidateVisual() unconditionally here: when both axes are in
                // scroll mode, the image is at a fixed content position (PanPadding, PanPadding);
                // the ScrollViewer's compositor update handles the visual shift without a fresh render.
            }
            else
            {
                // Use viewport-space coordinates (same reference as _panAnchor, which
                // was captured from e.GetPosition(_scrollViewer) in OnPointerPressed).
                // e.GetPosition(this) is content-space; when a deferred scroll reset
                // hasn't fired yet the two spaces differ by the stale scroll offset,
                // causing an immediate pan jump on the first move event.
                var freePanPos = _scrollViewer != null ? e.GetPosition(_scrollViewer) : pos;
                (_panX, _panY) = WireframeTransform.Pan(
                    _panAnchorX, _panAnchorY,
                    (float)_panAnchor.X, (float)_panAnchor.Y,
                    (float)freePanPos.X, (float)freePanPos.Y);
                InvalidateVisual();
            }
            return;
        }

        if (_draggingRect != null)
        {
            ApplyHandleDrag(pos);
            return;
        }

        UpdateHoverCursor(pos);

        // Update hover preview for magic-wand / grid-snap
        UpdatePreview(pos);
    }

    private void UpdateHoverCursor(Point pos)
    {
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
            e.Pointer.Capture(null);
        }

        if (_draggingRect != null)
        {
            FrameRegionChanged?.Invoke(_draggingRect.Frame);
            _draggingRect = null;
            _draggingHandle = HandleKind.None;
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

    private void StartPan(Point pos)
    {
        _isPanning = true;
        _panAnchor = pos;
        _dbgPanMoveCount = 0;
        bool ovX = OverflowX();
        bool ovY = OverflowY();
        if (ovX || ovY) // UseScrollPan()
        {
            // For overflowing axes: anchor the scroll offset so UpdatePan can compute deltas.
            // For non-overflowing axes: anchor the free-pan position so UpdatePan adjusts _panX/Y directly.
            // If a post-layout scroll is pending, use the pending (intended) values for scroll axes
            // and cancel the pending apply so the deferred Post won't override the user's pan.
            if (_pendingScrollApply)
            {
                _scrollAnchorX = ovX ? _pendingScrollX : 0f;
                _scrollAnchorY = ovY ? _pendingScrollY : 0f;
                _pendingScrollApply = false;
                _pendingScrollGeneration++;
                DebugLog("START_PAN",
                    $"[PENDING] ovX={ovX} ovY={ovY} anchorX={_scrollAnchorX:F1} anchorY={_scrollAnchorY:F1} " +
                    $"pos=({pos.X:F1},{pos.Y:F1}) cleared pendingApply");
            }
            else
            {
                _scrollAnchorX = ovX ? _scrollTargetX : 0f;
                _scrollAnchorY = ovY ? _scrollTargetY : 0f;
                DebugLog("START_PAN",
                    $"ovX={ovX} ovY={ovY} anchorX={_scrollAnchorX:F1} anchorY={_scrollAnchorY:F1} " +
                    $"sv.Offset.X={_scrollViewer?.Offset.X:F1} pos=({pos.X:F1},{pos.Y:F1})");
            }
            if (!ovX) _panAnchorX = _panX;
            if (!ovY) _panAnchorY = _panY;
        }
        else
        {
            _panAnchorX = _panX;
            _panAnchorY = _panY;
            DebugLog("START_PAN",
                $"[FREE-PAN] anchorXY=({_panAnchorX:F1},{_panAnchorY:F1}) pos=({pos.X:F1},{pos.Y:F1})");
        }
    }

    private void ZoomToward(float sx, float sy, float factor)
    {
        float oldZoom = _zoom;
        var (newPanX, newPanY, newZoom) = WireframeTransform.ZoomToward(sx, sy, factor, _panX, _panY, _zoom);
        _zoom = newZoom;

        if (_scrollViewer != null)
        {
            // Read from _scrollTargetX/Y (synchronously maintained) rather than
            // _scrollViewer.Offset (deferred via Dispatcher.Post).  This ensures
            // rapid wheel events each chain off the correct accumulated target
            // instead of repeatedly reading the still-zero visual offset.
            float scrollX = _scrollTargetX;
            float scrollY = _scrollTargetY;
            var vp = _scrollViewer.Viewport;

            // Per-axis mode: in scroll mode (ScrollViewer attached with valid viewport) both axes
            // always use scroll — padding is always applied so the scrollbars stay active.
            // Guard vp.Width/Height > 1 to avoid premature scroll mode on uninitialized viewports.
            bool overflowX = vp.Width  > 1 && _bitmap != null;
            bool overflowY = vp.Height > 1 && _bitmap != null;

            // Compute effective padding for the new zoom level (already stored in _zoom).
            float epX = EffectivePaddingX();
            float epY = EffectivePaddingY();

            // Scroll axes: derive the required scroll offset so the zoom pivot stays fixed.
            // The image is at content position (epX, epY); pivot preservation gives:
            //   newScrollX = scrollX - newPanX + epX
            float newScrollX = overflowX ? Math.Max(0f, scrollX - newPanX + epX) : 0f;
            float newScrollY = overflowY ? Math.Max(0f, scrollY - newPanY + epY) : 0f;

            _panX = overflowX ? epX : newPanX - scrollX;
            _panY = overflowY ? epY : newPanY - scrollY;

            DebugLog("ZOOM_TOWARD",
                $"factor={factor:F3} pivot=({sx:F1},{sy:F1}) zoom={oldZoom:F3}→{_zoom:F3} " +
                $"ovX={overflowX} ovY={overflowY} scrollX={scrollX:F1} " +
                $"newScrollX={newScrollX:F1} vpW={vp.Width:F1} " +
                $"imgW*zoom={(_bitmap?.Width ?? 0) * _zoom:F1}");

            // Queue the scroll to be applied AFTER the layout pass.
            // Using LayoutUpdated instead of Dispatcher.Post(Render) is critical:
            // in Avalonia's live render loop Render (priority 7) runs BEFORE Layout
            // (priority 5), so a Post(Render) would fire before the content has grown
            // to its new size and would be clamped to 0, resetting _scrollTargetX/Y
            // to 0 and locking panning.  LayoutUpdated fires synchronously after
            // ArrangeCore completes, at which point the content extent is correct.
            // QueueScrollAfterLayout also updates _scrollTargetX/Y for rapid-zoom
            // chaining so this replaces the explicit assignments.
            QueueScrollAfterLayout(newScrollX, newScrollY);
            InvalidateMeasure();
        }
        else
        {
            _panX = newPanX;
            _panY = newPanY;
        }

        InvalidateVisual();
        ZoomChanged?.Invoke(_zoom * 100f);
    }

    private void ApplyHandleDrag(Point pos)
    {
        if (_draggingRect is null || _bitmap is null) return;

        var world = ScreenToTexture((float)pos.X, (float)pos.Y);
        float dx = world.X - _dragStartWorld.X;
        float dy = world.Y - _dragStartWorld.Y;
        var startBounds = new BoundsRect(_dragStartBounds.Left, _dragStartBounds.Top,
                                         _dragStartBounds.Right, _dragStartBounds.Bottom);

        var nb = DragHandleApplier.Apply(_draggingHandle, dx, dy, startBounds,
                                         _bitmap.Width, _bitmap.Height);

        // Snap moved edges to the grid when the grid is enabled
        if (_showGrid && _gridSize > 0)
            nb = SnapBoundsToGrid(nb, _draggingHandle, _gridSize);

        _draggingRect.Bounds = new SKRect(nb.Left, nb.Top, nb.Right, nb.Bottom);

        // Write UV coords back to the frame
        var (l, t, r, b) = DragHandleApplier.ToUvCoords(nb, _bitmap.Width, _bitmap.Height);
        var f = _draggingRect.Frame;
        f.LeftCoordinate   = l;
        f.RightCoordinate  = r;
        f.TopCoordinate    = t;
        f.BottomCoordinate = b;

        // Live update for the property panel (no save / tree refresh yet)
        FrameLiveUpdated?.Invoke(_draggingRect.Frame);
        InvalidateVisual();
    }

    /// <summary>
    /// Snaps only the edges that <paramref name="handle"/> controls to the grid,
    /// leaving unaffected edges unchanged.
    /// </summary>
    private static BoundsRect SnapBoundsToGrid(BoundsRect nb, HandleKind handle, int gridSize)
    {
        float Snap(float v) => MathF.Round(v / gridSize) * gridSize;

        float l = nb.Left, t = nb.Top, r = nb.Right, b = nb.Bottom;
        switch (handle)
        {
            case HandleKind.Move:
                // Preserve size; snap top-left corner
                float snappedL = Snap(l);
                float snappedT = Snap(t);
                return new BoundsRect(snappedL, snappedT,
                                      snappedL + (r - l), snappedT + (b - t));
            case HandleKind.TopLeft:   return new BoundsRect(Snap(l), Snap(t), r, b);
            case HandleKind.TopCenter: return new BoundsRect(l, Snap(t), r, b);
            case HandleKind.TopRight:  return new BoundsRect(l, Snap(t), Snap(r), b);
            case HandleKind.MidLeft:   return new BoundsRect(Snap(l), t, r, b);
            case HandleKind.MidRight:  return new BoundsRect(l, t, Snap(r), b);
            case HandleKind.BotLeft:   return new BoundsRect(Snap(l), t, r, Snap(b));
            case HandleKind.BotCenter: return new BoundsRect(l, t, r, Snap(b));
            case HandleKind.BotRight:  return new BoundsRect(l, t, Snap(r), Snap(b));
            default:                   return nb;
        }
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
        if (sel is null) return (null, HandleKind.None);

        var sr = ToScreen(sel.Bounds);

        var kind = DragHandleHitTester.GetHandleAt(
            (float)pos.X, (float)pos.Y,
            sr.Left, sr.Top, sr.Right, sr.Bottom);

        return kind == HandleKind.None ? (null, HandleKind.None) : (sel, kind);
    }

    private void TrySelectFrameAtPoint(SKPoint worldPt)
    {
        foreach (var fr in _frameRects)
        {
            if (fr.Bounds.Contains(worldPt))
            {
                SelectedState.Self.SelectedFrame = fr.Frame;
                return;
            }
        }
    }

    private void ApplyRegionToSelectedFrame(int minX, int minY, int maxX, int maxY)
    {
        if (SelectedState.Self.SelectedFrame is null || _bitmap is null) return;
        var frame = SelectedState.Self.SelectedFrame;
        float w = _bitmap.Width, h = _bitmap.Height;
        frame.LeftCoordinate   = minX / w;
        frame.RightCoordinate  = maxX / w;
        frame.TopCoordinate    = minY / h;
        frame.BottomCoordinate = maxY / h;
        RefreshFramesInternal();
        FrameRegionChanged?.Invoke(frame);
    }

    // ── Coordinate transforms ─────────────────────────────────────────────────

    private SKPoint ScreenToTexture(float sx, float sy)
    {
        var (tx, ty) = WireframeTransform.ScreenToTexture(sx, sy, _panX, _panY, _zoom);
        return new SKPoint(tx, ty);
    }

    private SKRect ToScreen(SKRect r)
    {
        var (l, t, rr, b) = WireframeTransform.TextureRectToScreen(
            r.Left, r.Top, r.Right, r.Bottom, _panX, _panY, _zoom);
        return new SKRect(l, t, rr, b);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshFramesInternal()
    {
        _frameRects.Clear();

        if (_bitmap is null) { InvalidateVisual(); return; }

        var selectedFrame  = SelectedState.Self.SelectedFrame;
        var selectedChain  = SelectedState.Self.SelectedChain;
        var selectedChains = SelectedState.Self.SelectedChains;

        string? achxFolder = string.IsNullOrEmpty(ProjectManager.Self.FileName)
            ? null
            : FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName);

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
                var fp = new FilePath(achxFolder + frame.TextureName);
                if (!fp.Equals(new FilePath(_loadedTexturePath))) continue;
            }

            float pixL = frame.LeftCoordinate   * w;
            float pixT = frame.TopCoordinate    * h;
            float pixR = frame.RightCoordinate  * w;
            float pixB = frame.BottomCoordinate * h;

            if (_showGrid && _gridSize > 0)
            {
                // Snap the display coordinates to grid line intersections.
                // UV write-back is intentionally omitted here; it is the sole
                // responsibility of ApplyGridSnap (called from SetGrid).
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

        float ctrlW, ctrlH;
        if (_scrollViewer != null && _scrollViewer.Viewport.Width > 1)
        {
            ctrlW = (float)_scrollViewer.Viewport.Width;
            ctrlH = (float)_scrollViewer.Viewport.Height;
        }
        else
        {
            ctrlW = (float)(Bounds.Width  > 0 ? Bounds.Width  : 800);
            ctrlH = (float)(Bounds.Height > 0 ? Bounds.Height : 600);
        }

        (_panX, _panY, _zoom) = WireframeTransform.CenterFit(_bitmap.Width, _bitmap.Height, ctrlW, ctrlH);

        // After CenterFit the image is centred inside the viewport.  In scroll mode we
        // always apply padding so the scrollbars are active — set panX/panY to
        // EffectivePaddingX/Y() and scroll to the corresponding centred offset so the
        // image appears centred just as CenterFit computed it.
        if (_scrollViewer != null)
        {
            // _zoom is now set; compute effective padding at this zoom level.
            float epX = EffectivePaddingX();
            float epY = EffectivePaddingY();

            // Content-space: image left edge at epX.  Center the image in the viewport:
            //   centreScrollX = epX + (bitmapW * zoom) / 2 - ctrlW / 2
            // When image < viewport: epX ≈ (ctrlW - imgW*zoom)/2 + ExtraScrollable
            //   → centreScrollX ≈ ExtraScrollable
            float centreScrollX = Math.Max(0f, epX + _bitmap.Width  * _zoom / 2f - ctrlW / 2f);
            float centreScrollY = Math.Max(0f, epY + _bitmap.Height * _zoom / 2f - ctrlH / 2f);

            _panX = epX;
            _panY = epY;

            // Cancel any pending zoom-scroll (would override centering) and queue
            // the centred offset to be applied after the layout pass.
            CancelPendingScrollApply(x: centreScrollX, y: centreScrollY);
            QueueScrollAfterLayout(centreScrollX, centreScrollY);
        }

        InvalidateMeasure();
    }

    private string? DetermineTexturePath()
    {
        string? textureName = SelectedState.Self.SelectedFrame?.TextureName
                           ?? SelectedState.Self.SelectedChain?.Frames?.FirstOrDefault()?.TextureName;

        if (string.IsNullOrEmpty(textureName))
            return null;

        // If no ACHX is saved yet, the texture path is already absolute.
        if (string.IsNullOrEmpty(ProjectManager.Self.FileName))
            return textureName;

        return FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName) + textureName;
    }
}
