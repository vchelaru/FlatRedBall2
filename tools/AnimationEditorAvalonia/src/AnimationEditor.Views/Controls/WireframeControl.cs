using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnimationEditor.App.Services;
using AnimationEditor.Core.DragDrop;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Avalonia + SkiaSharp wireframe editor.
/// Replaces ImageRegionSelectionControl + WireframeManager from the WinForms port.
/// <para>
/// Derives from <see cref="TextureViewport"/>, which owns all editing-agnostic viewport behavior
/// (texture load, pan/zoom camera, grid, canvas palette, diagnostics, middle-mouse pan). This
/// control adds the animation-frame editing layer on top: frame region rectangles, resize handles,
/// chain/handle dragging, magic-wand and grid frame creation, and the origin crosshair.
/// </para>
/// </summary>
public class WireframeControl : TextureViewport
{
    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class FrameRect
    {
        public AnimationFrameSave Frame = null!;
        public SKRect Bounds;       // texture-space pixel coords
        public bool IsSelected;
    }

    /// <summary>
    /// Wireframe editing overlay drawn on top of the base texture/grid layers. Carries the frame
    /// rectangles, resize-handle bounds, magic-wand/grid preview, pending-cut frames, and origin
    /// crosshair — all captured on the UI thread so <see cref="DrawWireframeOverlay"/> can draw
    /// them safely on the render thread.
    /// </summary>
    private sealed class WireframeSnapshot : TextureViewportSnapshot
    {
        public List<(SKRect Bounds, bool IsSelected)> Frames = new();
        public SKRect? SelectedHandleBounds;    // null → no handles drawn
        public bool ShowPreview;
        public SKRect PreviewRect;
        /// <summary>
        /// Texture-space position (pixels) of the entity origin for the selected frame.
        /// Null when no frame is selected or origin data is unavailable.
        /// </summary>
        public float? OriginTexX, OriginTexY;
        public List<SKRect> PendingCutFrameBounds = new();
    }

    private static readonly SKColor CutOutlineColor = new(224, 112, 48, 220);

    // ── Overlay rendering ─────────────────────────────────────────────────────

    // Draws the editing overlay (frames → pending-cut → handles → preview → origin) on top of the
    // base texture/grid layers. Runs on the render thread from immutable snapshot data — reads only
    // WireframeSnapshot fields (never live control state), so it can never race the UI thread.
    private static void DrawWireframeOverlay(SKCanvas canvas, TextureViewportSnapshot snapshot)
    {
        var s = (WireframeSnapshot)snapshot;

        // Frame region rectangles
        using var frameFill = new SKPaint { Style = SKPaintStyle.Fill };
        using var frameStroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

        foreach (var (bounds, isSelected) in s.Frames)
        {
            var sr = SnapToScreen(bounds, s);
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

        // Pending-cut frames: dashed orange overlay (distinct from selection blue).
        if (s.PendingCutFrameBounds.Count > 0)
        {
            using var cutPaint = new SKPaint
            {
                Color = CutOutlineColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                PathEffect = SKPathEffect.CreateDash(new float[] { 6f, 4f }, 0f),
            };
            foreach (var bounds in s.PendingCutFrameBounds)
                canvas.DrawRect(SnapToScreen(bounds, s), cutPaint);
        }

        // Resize handles on selected frame
        if (s.SelectedHandleBounds.HasValue)
            DrawHandles(canvas, SnapToScreen(s.SelectedHandleBounds.Value, s));

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
            canvas.DrawRect(SnapToScreen(s.PreviewRect, s), pvPaint);
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

    // Texture-space rect → screen-space, using the snapshot's captured camera (render-thread safe).
    // Named distinctly from the inherited instance ToScreen(SKRect) so both can coexist.
    private static SKRect SnapToScreen(SKRect r, TextureViewportSnapshot s)
    {
        var (l, t, rr, b) = CanvasTransform.TextureRectToScreen(
            r.Left, r.Top, r.Right, r.Bottom, s.PanX, s.PanY, s.Zoom);
        return new SKRect(l, t, rr, b);
    }

    // ── Fields ────────────────────────────────────────────────────────────────

    private InspectableImage? _inspectableImage;

    // ── Drag auto-pan (#540) ───────────────────────────────────────────────────
    // While a handle/chain drag is active, the timer nudges the camera each tick via
    // StepAutoPan whenever the last-known pointer position sits within AutoPanMarginPx of the
    // viewport edge, then re-applies the drag at that same screen position — ScreenToTexture
    // depends on the camera pan, so re-running Apply*Drag after the pan shifts yields a new
    // texture-space delta and the dragged frame keeps tracking the cursor.
    private DispatcherTimer? _autoPanTimer;
    private Point _lastPointerPos;
    private const float AutoPanMarginPx = 32f;
    private const float AutoPanMaxSpeedPxPerSec = 900f;
    private const float AutoPanIntervalSeconds = 1f / 60f;

    private readonly List<FrameRect> _frameRects = new();

    /// <summary>
    /// The single "primary" selected frame's rect — used for resize handles and
    /// handle-drag hit-testing. Resolved by reference to <see cref="ISelectedState.SelectedFrame"/>
    /// rather than any <see cref="FrameRect.IsSelected"/> flag, because a tree multi-select
    /// (issue #582) marks every selected frame's rect IsSelected for preview highlighting, but
    /// resize handles/dragging still only ever target one frame at a time.
    /// <para>
    /// Returns null when more than one frame is multi-selected: handles on just one of several
    /// equally-selected frames implies that frame is somehow special, so they are suppressed
    /// entirely (same treatment as a whole-chain selection with no individual frame chosen).
    /// </para>
    /// </summary>
    private FrameRect? PrimaryFrameRect()
    {
        if ((_selectedState?.SelectedFrames.Count ?? 0) > 1)
            return null;

        return _selectedState?.SelectedFrame is { } f
            ? _frameRects.FirstOrDefault(fr => fr.Frame == f)
            : null;
    }

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

    // ── Public properties ─────────────────────────────────────────────────────

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
    /// Fired when the user ctrl+clicks to add a new frame
    /// (minX, minY, maxX, maxY in texture pixel coords).
    /// </summary>
    public event Action<int, int, int, int>? FrameCreatedFromRegion;

    /// <summary>
    /// Set by <c>MainWindow</c> to apply a PNG dropped onto the canvas — the same path used by
    /// the ANIMATIONS tree's PNG drop (issue #560): (targetChain, targetFrame, droppedFilePath,
    /// ctrlHeld) → true if the drop was applied. Left null in standalone/test contexts, where
    /// a drop is simply ignored.
    /// </summary>
    public Func<AnimationChainSave?, AnimationFrameSave?, string, bool, Task<bool>>? HandlePngDrop { get; set; }

    // ── Injected services ─────────────────────────────────────────────────────

    private ISelectedState? _selectedState;
    private IAppState? _appState;
    private IPendingCutState? _pendingCutState;
    private IAppCommands? _appCommands;
    private IApplicationEvents? _events;
    private IProjectManager? _projectManager;
    private IUndoManager? _undoManager;
    private Action<string>? _showError;
    private ThumbnailService? _thumbnailService;

    /// <summary>
    /// Called from MainWindow after DI container wires all services.
    /// Moves subscriptions out of the constructor so services are available.
    /// </summary>
    /// <param name="thumbnailService">
    /// Optional. When supplied, <see cref="RefreshAll"/> resolves the current texture through
    /// it (bare-name lookup against its cache first, falling back to disk) instead of always
    /// reading straight from disk -- the seam the browser-wasm build needs (#614), since it has
    /// no filesystem but already has every dropped/picked texture decoded via
    /// <see cref="ThumbnailService.SeedTexture"/>. Left <c>null</c> on desktop, where reading the
    /// resolved path from disk (the pre-#614 behavior) is unchanged.
    /// </param>
    public void InitializeServices(
        ISelectedState selectedState,
        IAppState appState,
        IAppCommands appCommands,
        IApplicationEvents events,
        IProjectManager projectManager,
        IUndoManager undoManager,
        IPendingCutState pendingCutState,
        Action<string>? showError = null,
        ThumbnailService? thumbnailService = null)
    {
        _selectedState   = selectedState;
        _appState        = appState;
        _pendingCutState = pendingCutState;
        _appCommands     = appCommands;
        _events          = events;
        _projectManager  = projectManager;
        _undoManager     = undoManager;
        _showError       = showError;
        _thumbnailService = thumbnailService;

        _selectedState.SelectionChanged     += () => Dispatcher.UIThread.InvokeAsync(RefreshAll);
        _pendingCutState.Changed            += () => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
        _appCommands.RefreshWireframeRequested += () => Dispatcher.UIThread.InvokeAsync(RefreshAll);
        _events.AchxLoaded                  += _ => Dispatcher.UIThread.InvokeAsync(RefreshAll);
        // Post at Background priority so this runs *after* the add/retexture's higher-priority
        // SelectionChanged → RefreshAll (or a synchronous LoadTexture) has loaded the texture — only
        // then can we measure the frame against the viewport and fit it if it's too big (#616). Mirrors
        // the double-click CenterOnFrame post in MainWindow.
        _events.FitFrameToViewRequested     += () => Dispatcher.UIThread.Post(FitSelectedFrameIfLargerThanViewport, DispatcherPriority.Background);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public WireframeControl()
    {
        // Right-click opens this menu with a "View <filename> in Explorer" item for the
        // currently loaded texture (mirrors PreviewControl's context menu — issue #573).
        var contextMenu = new ContextMenu();
        contextMenu.Opening += OnContextMenuOpening;
        ContextMenu = contextMenu;

        // PNG drop (issue #560) — same DragOver/Drop plumbing as the ANIMATIONS tree
        // (OnTreeDragOver/OnTreeDrop in MainWindow), targeting the current selection instead
        // of a hovered tree node.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnPngDragOver);
        AddHandler(DragDrop.DropEvent, OnPngDrop);

        // Base ctor sets up ClipToBounds/Focusable, theme repaint, SizeChanged re-clamp, and the
        // timer teardown. Service subscriptions are deferred to InitializeServices (from MainWindow).
    }

    // ── PNG drop ──────────────────────────────────────────────────────────────

    private void OnPngDragOver(object? sender, DragEventArgs e)
    {
        var firstFile = DragDropFileResolver.GetFirstDroppedFilePath(e);

        var wouldApply = !string.IsNullOrEmpty(firstFile) &&
            TextureDropProcessor.ComputePngDrop(
                _selectedState?.SelectedChain,
                _selectedState?.SelectedFrame,
                firstFile,
                _projectManager?.FileName,
                e.KeyModifiers.HasFlag(KeyModifiers.Control)).Result
            != TextureDropResult.NotApplied;

        e.DragEffects = wouldApply ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnPngDrop(object? sender, DragEventArgs e)
    {
        var firstFile = DragDropFileResolver.GetFirstDroppedFilePath(e);
        if (string.IsNullOrEmpty(firstFile) || HandlePngDrop is null)
            return;

        // SelectedFrame's setter keeps SelectedChain in sync with its parent, so this already
        // falls back to the selected chain (whole-chain retexture/create-first-frame) the same
        // way SyncTextureCombo's texture-preview lookup does when no frame is selected.
        var applied = await HandlePngDrop(
            _selectedState?.SelectedChain, _selectedState?.SelectedFrame,
            firstFile, e.KeyModifiers.HasFlag(KeyModifiers.Control));

        if (applied)
            e.Handled = true;
    }

    // ── Drag auto-pan (#540) ────────────────────────────────────────────────────

    private void StartAutoPanTimer()
    {
        _autoPanTimer ??= CreateAutoPanTimer();
        _autoPanTimer.Start();
    }

    private void StopAutoPanTimer() => _autoPanTimer?.Stop();

    private DispatcherTimer CreateAutoPanTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutoPanIntervalSeconds) };
        timer.Tick += (_, _) => StepAutoPan(AutoPanIntervalSeconds);
        return timer;
    }

    /// <summary>
    /// Nudges the camera toward <see cref="_lastPointerPos"/> when it sits within
    /// <see cref="AutoPanMarginPx"/> of the viewport edge during an active handle or chain drag
    /// (<see cref="CanvasTransform.AutoPanVelocity"/>), then re-applies the drag at that same
    /// screen position so the dragged frame keeps tracking the cursor. Live-driven by
    /// <see cref="_autoPanTimer"/>; tests call this directly for determinism (see
    /// <see cref="TextureViewport.StepZoomAnimation"/> for the same pattern). No-op with no active
    /// drag, no bitmap, or before layout has produced a real viewport.
    /// </summary>
    public void StepAutoPan(float dtSeconds)
    {
        if (_bitmap is null || Bounds.Width <= 1) return;
        if (_draggingRect is null && !_draggingChain) return;

        var (vx, vy) = CanvasTransform.AutoPanVelocity(
            (float)_lastPointerPos.X, (float)_lastPointerPos.Y,
            (float)Bounds.Width, (float)Bounds.Height,
            AutoPanMarginPx, AutoPanMaxSpeedPxPerSec);
        if (vx == 0f && vy == 0f) return;

        _panX += vx * dtSeconds;
        _panY += vy * dtSeconds;
        ClampCamera();

        if (_draggingRect != null) ApplyHandleDrag(_lastPointerPos);
        else ApplyChainDrag(_lastPointerPos);

        InvalidateVisual();
        RaiseViewChanged();
    }

    // ── Frame refresh ─────────────────────────────────────────────────────────

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

        SKBitmap? known = null;
        if (_thumbnailService != null)
        {
            var frame = _selectedState?.SelectedFrame ?? _selectedState?.SelectedChain?.Frames?.FirstOrDefault();
            var resolvedPath = _thumbnailService.ResolveTexturePath(frame);
            if (resolvedPath != null)
                known = _thumbnailService.GetBitmap(resolvedPath);
        }

        LoadTexture(path, known);
    }

    /// <inheritdoc />
    protected override void OnTextureLoaded(SKBitmap? bitmap)
    {
        // Magic-wand flood-fill needs an inspectable pixel copy; rebuild it for the new bitmap
        // while the mode is active, else drop any stale one (it's only read in magic-wand mode).
        _inspectableImage = _isMagicWandMode && bitmap != null
            ? new InspectableImage(bitmap)
            : null;
        RefreshFramesInternal();
    }

    // ── Frame-region editing (test API) ───────────────────────────────────────

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
        SnapSelectedFrameToGridCell(world.X, world.Y);
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
        var sel = PrimaryFrameRect();
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
    /// Test-only: begins a handle-drag gesture without applying an end position, mirroring the
    /// handle-hit branch of <see cref="OnEditPointerPressed"/>. Pairs with
    /// <see cref="SimulateDragPointerMove"/> and <see cref="StepAutoPan"/> to observe auto-pan
    /// (#540) mid-drag; use <see cref="SimulateHandleDrag"/> instead for a one-shot gesture that
    /// doesn't need intermediate ticks. No-op when no frame is selected or no texture is loaded.
    /// </summary>
    public void SimulateHandleDragBegin(HandleKind handle, float startScreenX, float startScreenY)
    {
        var sel = PrimaryFrameRect();
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
        _lastPointerPos = new Point(startScreenX, startScreenY);
    }

    /// <summary>
    /// Test-only: begins a chain-drag gesture without applying an end position — the
    /// chain-drag counterpart of <see cref="SimulateHandleDragBegin"/>. No-op when no chain is
    /// selected, no frames are visible, or no texture is loaded.
    /// </summary>
    public void SimulateChainDragBegin(float startScreenX, float startScreenY)
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
        _lastPointerPos = new Point(startScreenX, startScreenY);
    }

    /// <summary>
    /// Test-only: continues the handle/chain drag started by <see cref="SimulateHandleDragBegin"/>
    /// or <see cref="SimulateChainDragBegin"/> to the given <b>viewport-space</b> pointer
    /// position, mirroring the dragging branch of <see cref="OnEditPointerMoved"/>. No-op when no
    /// drag is active.
    /// </summary>
    public void SimulateDragPointerMove(float vpX, float vpY)
    {
        _lastPointerPos = new Point(vpX, vpY);
        if (_draggingRect != null) ApplyHandleDrag(_lastPointerPos);
        else if (_draggingChain) ApplyChainDrag(_lastPointerPos);
    }

    /// <summary>
    /// Test-only: ends the drag started by <see cref="SimulateHandleDragBegin"/> or
    /// <see cref="SimulateChainDragBegin"/>, without recording undo or firing region-changed
    /// events — tests that need those should use the one-shot <see cref="SimulateHandleDrag"/>/
    /// <see cref="SimulateChainDrag"/> instead.
    /// </summary>
    public void SimulateDragEnd()
    {
        _draggingRect    = null;
        _draggingHandle  = HandleKind.None;
        _bulkHandleDragStarts.Clear();
        _draggingChain   = false;
        _chainDragStarts.Clear();
    }

    // ── Frame centering ───────────────────────────────────────────────────────

    /// <summary>Fraction of the viewport a fitted frame fills, leaving a margin so its handles read
    /// clearly (matches the 85 % of <see cref="CanvasTransform.CenterFit"/>).</summary>
    private const float FrameFitFraction = 0.85f;

    /// <summary>
    /// Fits the currently selected frame into view via <see cref="FitFrameIfLargerThanViewport"/>.
    /// This is what <see cref="ApplicationEvents.FitFrameToViewRequested"/> triggers (posted at
    /// Background priority so the texture is loaded first). No-op when nothing is selected.
    /// </summary>
    private void FitSelectedFrameIfLargerThanViewport()
    {
        if (_selectedState?.SelectedFrame is { } frame)
            FitFrameIfLargerThanViewport(frame);
    }

    /// <summary>
    /// Zoom-to-fits <paramref name="frame"/> into view, but <em>only</em> when it is currently too
    /// large to fit the viewport (<see cref="CanvasTransform.RectExceedsViewport"/>) — otherwise the
    /// zoom is left untouched, so this never surprises a user whose frame already fits. The #616 fix:
    /// a newly added or retextured frame covering a large sheet would otherwise be selected with all
    /// of its edges and handles off-screen, leaving nothing on the canvas to show the selection.
    /// Raises <see cref="TextureViewport.ZoomChanged"/> when it changes the zoom so the zoom combo
    /// re-syncs. No-op when no bitmap is loaded or layout hasn't produced a viewport yet.
    /// </summary>
    public void FitFrameIfLargerThanViewport(AnimationFrameSave frame)
    {
        if (_bitmap is null) return;
        float vpW = (float)Bounds.Width;
        float vpH = (float)Bounds.Height;
        if (vpW <= 1 || vpH <= 1) return;

        float bmpW = _bitmap.Width;
        float bmpH = _bitmap.Height;
        float pixL = frame.LeftCoordinate  * bmpW;
        float pixT = frame.TopCoordinate   * bmpH;
        float pixR = frame.RightCoordinate * bmpW;
        float pixB = frame.BottomCoordinate * bmpH;

        if (!CanvasTransform.RectExceedsViewport(pixR - pixL, pixB - pixT, _zoom, vpW, vpH))
            return;

        CancelZoomAnimation();   // this fit overrides any in-flight wheel ease
        // maxZoom 1: we only reach here when the frame exceeds the viewport, so fitting always zooms
        // out — cap at 100 % so a degenerate thin frame can never be magnified past native.
        (_panX, _panY, _zoom) = CanvasTransform.FitRect(pixL, pixT, pixR, pixB, vpW, vpH, FrameFitFraction, 1f);
        ClampCamera();
        InvalidateVisual();
        RaiseZoomChanged();
        RaiseViewChanged();
    }

    /// <summary>
    /// Pans so <paramref name="frame"/>'s centre lands at the viewport centre, preserving the
    /// current zoom level (the zoom is never changed, so <see cref="TextureViewport.ZoomChanged"/>
    /// does not fire). Clamped to the valid pan band, so a frame near the texture edge lands as
    /// close to centre as the dead-space allows. Does nothing when no bitmap is loaded.
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

    /// <summary>
    /// Test-only: exposes the drag/hover hit-test result at a screen-space point.
    /// Avoids asserting on the Avalonia <see cref="Control.Cursor"/> property, which
    /// exposes no equality on <c>StandardCursorType</c>.
    /// </summary>
    public HandleKind HitTestHandleKindAt(float screenX, float screenY) =>
        HitTestHandle(new Point(screenX, screenY)).handle;

    /// <inheritdoc />
    protected override TextureViewportSnapshot BuildSnapshot(double width, double height)
    {
        var snap = new WireframeSnapshot { DrawOverlay = DrawWireframeOverlay };
        PopulateBaseSnapshot(snap, width, height);
        snap.ShowPreview = _showPreview;
        snap.PreviewRect = _previewRect;

        foreach (var fr in _frameRects)
            snap.Frames.Add((fr.Bounds, fr.IsSelected));

        snap.PendingCutFrameBounds.AddRange(BuildPendingCutFrameBounds());

        var sel = PrimaryFrameRect();
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

    /// <inheritdoc />
    protected override void OnEditPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        var pos = e.GetPosition(this);
        bool isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;

        if (!props.IsLeftButtonPressed) return;

        // Grid mode double-click: bypass handle hit-testing so that a frame covering
        // the entire texture (which would otherwise always hit HandleKind.Move) can still
        // have a specific grid cell applied to it.
        if (!isCtrl && !_isMagicWandMode && e.ClickCount == 2 && _showGrid && _gridSize > 0 && _bitmap != null)
        {
            var dblWorld = ScreenToTexture((float)pos.X, (float)pos.Y);
            SnapSelectedFrameToGridCell(dblWorld.X, dblWorld.Y);
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
                _lastPointerPos = pos;
                StartAutoPanTimer();
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

        // 3. Grid mode: Ctrl+click → create a new cell-sized frame; plain click →
        //    reposition the selected frame's origin to the cell, preserving its size.
        if (_showGrid && _gridSize > 0)
        {
            if (isCtrl)
            {
                int gx = GridSnapper.Snap(world.X, _gridSize);
                int gy = GridSnapper.Snap(world.Y, _gridSize);
                FrameCreatedFromRegion?.Invoke(gx, gy, gx + _gridSize, gy + _gridSize);
            }
            else
                SnapSelectedFrameToGridCell(world.X, world.Y);
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

    /// <inheritdoc />
    protected override void OnEditPointerMoved(PointerEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (_draggingRect != null)
        {
            _lastPointerPos = pos;
            ApplyHandleDrag(pos);
            return;
        }

        if (_draggingChain)
        {
            _lastPointerPos = pos;
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

    /// <inheritdoc />
    protected override void OnEditPointerReleased(PointerReleasedEventArgs e)
    {
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
            StopAutoPanTimer();
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
            StopAutoPanTimer();
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
        var sel = PrimaryFrameRect();
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

        // Single chain or multi-chain composite Move handle: grab any visible frame to
        // drag the whole group together. Tested against each individual frame's actual
        // body (not the union bounding box, and no handle-offset expansion) so a point
        // in a gap between non-tiling frames — even a narrow gap between two frames
        // whose expanded handle-hit zones would otherwise overlap — is correctly not a
        // hit (issue #587). All hits are treated as Move since resizing the group via
        // the bounding rect is not supported, so there's no need to test handle zones.
        if (_selectedState?.SelectedChain != null && _frameRects.Count > 0)
        {
            bool overAnyFrame = _frameRects.Any(fr =>
            {
                var sr = ToScreen(fr.Bounds);
                return pos.X >= sr.Left && pos.X <= sr.Right &&
                       pos.Y >= sr.Top  && pos.Y <= sr.Bottom;
            });

            if (overAnyFrame)
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
    /// Grid click-to-place: snaps the selected frame's origin to the grid cell
    /// containing (<paramref name="worldX"/>, <paramref name="worldY"/>) while
    /// preserving its current pixel size. Grid must never resize an existing
    /// frame — only reposition it (issue #538).
    /// </summary>
    private void SnapSelectedFrameToGridCell(float worldX, float worldY)
    {
        if (_selectedState!.SelectedFrame is null || _bitmap is null) return;
        var frame = _selectedState!.SelectedFrame;
        float w = _bitmap.Width, h = _bitmap.Height;
        int frameW = (int)MathF.Round(frame.RightCoordinate  * w) - (int)MathF.Round(frame.LeftCoordinate * w);
        int frameH = (int)MathF.Round(frame.BottomCoordinate * h) - (int)MathF.Round(frame.TopCoordinate  * h);
        var (minX, minY, maxX, maxY) = GridPlacementCalculator.SnapOriginPreserveSize(
            worldX, worldY, _gridSize, frameW, frameH);
        ApplyRegionToSelectedFrame(minX, minY, maxX, maxY);
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshFramesInternal()
    {
        _frameRects.Clear();

        if (_bitmap is null) { InvalidateVisual(); return; }

        var selectedFrame  = _selectedState!.SelectedFrame;
        var selectedFrames = _selectedState!.SelectedFrames;
        var selectedChain  = _selectedState!.SelectedChain;
        var selectedChains = _selectedState!.SelectedChains;

        string? achxFolder = string.IsNullOrEmpty(_projectManager!.FileName)
            ? null
            : (Path.GetDirectoryName(_projectManager!.FileName) ?? string.Empty);

        // A tree multi-select of individual frames (issue #582) must show every selected
        // frame's region, not just the primary one — SelectedFrames already carries the full
        // multi-select bag (falling back to just [SelectedFrame] when nothing is multi-selected).
        IEnumerable<AnimationFrameSave> framesToShow;
        if (selectedFrames.Count > 1)
            framesToShow = selectedFrames;
        else if (selectedFrame != null)
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

            // Displayed bounds always reflect the frame's true UV pixel bounds.
            // Grid must never snap or resize an existing frame's display — it only
            // affects active drags and click-to-place (issue #538).
            float pixL = frame.LeftCoordinate   * w;
            float pixT = frame.TopCoordinate    * h;
            float pixR = frame.RightCoordinate  * w;
            float pixB = frame.BottomCoordinate * h;

            _frameRects.Add(new FrameRect
            {
                Frame      = frame,
                Bounds     = new SKRect(pixL, pixT, pixR, pixB),
                IsSelected = selectedFrames.Contains(frame)
            });
        }

        InvalidateVisual();
    }

    private List<SKRect> BuildPendingCutFrameBounds()
    {
        var result = new List<SKRect>();
        if (_pendingCutState is null || !_pendingCutState.IsActive || _bitmap is null)
            return result;

        string? achxFolder = string.IsNullOrEmpty(_projectManager!.FileName)
            ? null
            : (Path.GetDirectoryName(_projectManager!.FileName) ?? string.Empty);

        float w = _bitmap.Width;
        float h = _bitmap.Height;

        foreach (var frame in _pendingCutState.WireframeFrames)
        {
            if (string.IsNullOrEmpty(frame.TextureName)) continue;
            if (achxFolder != null && _loadedTexturePath != null)
            {
                var fp = new FilePath(Path.Combine(achxFolder, frame.TextureName));
                if (!fp.Equals(new FilePath(_loadedTexturePath))) continue;
            }

            float pixL = frame.LeftCoordinate   * w;
            float pixT = frame.TopCoordinate    * h;
            float pixR = frame.RightCoordinate  * w;
            float pixB = frame.BottomCoordinate * h;
            result.Add(new SKRect(pixL, pixT, pixR, pixB));
        }
        return result;
    }

    /// <summary>
    /// Rebuilds the ContextMenu with a single "View &lt;filename&gt; in Explorer" item for the
    /// currently loaded texture, or cancels the menu entirely when there's nothing to reveal.
    /// </summary>
    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var absPath = DetermineTexturePath();
        if (ContextMenu is not { } menu || absPath is null)
        {
            e.Cancel = true;
            return;
        }

        menu.Items.Clear();
        var item = new MenuItem { Header = $"View {new FilePath(absPath).NoPath} in Explorer" };
        item.Click += (_, _) =>
        {
            var error = ShellExplorer.RevealFile(absPath);
            if (error is not null) _showError?.Invoke(error);
        };
        menu.Items.Add(item);
    }

    internal string? DetermineTexturePath()
    {
        string? textureName = _selectedState!.SelectedFrame?.TextureName
                           ?? _selectedState!.SelectedChain?.Frames?.FirstOrDefault()?.TextureName;

        // When no *frame* is selected (an empty chain, or nothing selected), borrow the first texture
        // referenced anywhere in the project so the wireframe shows something to Ctrl+click on to seed
        // the first frame (issue #618). A selected frame with no texture is left blank instead of
        // borrowing another frame's — the canvas must not imply a texture the frame doesn't have (#616).
        if (string.IsNullOrEmpty(textureName) && _selectedState!.SelectedFrame is null)
            textureName = TextureListBuilder.GetFirstTextureName(_projectManager!.AnimationChainListSave);

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
    /// mirroring the double-click branch in <see cref="OnEditPointerPressed"/>.
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
