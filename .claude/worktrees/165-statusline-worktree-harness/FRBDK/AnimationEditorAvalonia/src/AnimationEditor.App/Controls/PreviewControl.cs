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
using FlatRedBall.Content.Math.Geometry;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FilePath = FlatRedBall.IO.FilePath;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Animated sprite preview panel. Plays the selected AnimationChain at runtime speed
/// (one frame = FrameLength seconds). When a single frame is selected, shows that
/// frame statically with optional onion-skin overlay.
/// </summary>
public class PreviewControl : Control
{
    // ── Animation state ───────────────────────────────────────────────────────
    private readonly DispatcherTimer _timer;
    private readonly AnimationEditor.Core.CommandsAndState.PlaybackController _playback = new();

    // ── Bitmap cache ──────────────────────────────────────────────────────────
    private readonly Dictionary<string, SKBitmap?> _bitmapCache =
        new(StringComparer.OrdinalIgnoreCase);

    // ── Camera ────────────────────────────────────────────────────────────────
    private float _zoom = 1f;
    private float _panX, _panY;

    // ── Settings ──────────────────────────────────────────────────────────────
    private bool _showOnionSkin;
    private bool _showGuides;

    // ── Pan drag ──────────────────────────────────────────────────────────────
    private bool  _isPanning;
    private Point _lastMousePt;

    // ── Rulers / guides ───────────────────────────────────────────────────────
    private const float RulerSize = 20f;
    private readonly List<float> _hGuides = new(); // world-Y values (positive = down on screen)
    private readonly List<float> _vGuides = new(); // world-X values (positive = right on screen)
    private int  _draggedGuideIdx = -1;
    private bool _draggingHGuide;                  // true = horizontal guide

    // ── Public properties ─────────────────────────────────────────────────────

    public bool ShowOnionSkin
    {
        get => _showOnionSkin;
        set { _showOnionSkin = value; InvalidateVisual(); }
    }

    public bool ShowGuides
    {
        get => _showGuides;
        set { _showGuides = value; InvalidateVisual(); }
    }

    public double SpeedMultiplier
    {
        get => _playback.SpeedMultiplier;
        set => _playback.SpeedMultiplier = value;
    }

    public void Play()  => _playback.Play();
    public void Pause() => _playback.Pause();
    public void StopPlayback() { _playback.Reset(); InvalidateVisual(); }

    /// <summary>
    /// Direct access to the playback state machine.
    /// Agents and tests can call <see cref="PlaybackController.Advance"/>,
    /// <see cref="PlaybackController.SetChain"/>, etc. without going through the timer.
    /// </summary>
    public PlaybackController Playback => _playback;

    /// <summary>
    /// Stops the internal timer so frame advancement is fully under caller control.
    /// Combine with <see cref="Playback"/>.<see cref="PlaybackController.Advance"/> for
    /// deterministic frame-level tests.
    /// </summary>
    public void PauseAutoPlayback()
    {
        _timer.Stop();
        _playback.Pause();
    }

    /// <summary>Restarts the internal 60 fps timer and resumes playback.</summary>
    public void ResumeAutoPlayback()
    {
        _playback.Play();
        _timer.Start();
    }

    /// <summary>
    /// Renders the current preview state to an off-screen bitmap of the given size.
    /// Uses the same frame-selection and cache-warming logic as the live render path.
    /// <para>
    /// Call <see cref="PauseAutoPlayback"/> first so the frame index stays stable
    /// while the bitmap is being assembled. Must be called on the UI thread.
    /// Caller is responsible for disposing the returned bitmap.
    /// </para>
    /// </summary>
    public SKBitmap RenderToBitmap(int width, int height)
    {
        var chain         = SelectedState.Self.SelectedChain;
        var selectedFrame = SelectedState.Self.SelectedFrame;

        AnimationFrameSave? displayFrame = null;
        AnimationFrameSave? onionFrame   = null;

        if (selectedFrame is not null)
        {
            displayFrame = selectedFrame;
            if (_showOnionSkin && chain is not null && chain.Frames.Count > 1)
            {
                int idx     = chain.Frames.IndexOf(selectedFrame);
                int prevIdx = (idx - 1 + chain.Frames.Count) % chain.Frames.Count;
                if (prevIdx != idx)
                    onionFrame = chain.Frames[prevIdx];
            }
        }
        else if (chain is not null && chain.Frames.Count > 0)
        {
            int idx  = Math.Clamp(_playback.CurrentFrameIndex, 0, chain.Frames.Count - 1);
            displayFrame = chain.Frames[idx];
        }

        string? texPath   = ResolveTexturePath(displayFrame);
        string? onionPath = ResolveTexturePath(onionFrame);
        GetBitmap(texPath);
        GetBitmap(onionPath);

        var snap   = new RenderSnapshot(displayFrame, onionFrame, _zoom, _panX, _panY,
                                        _showGuides, texPath, onionPath, width, height,
                                        AppState.Self.OffsetMultiplier,
                                        _hGuides.ToArray(), _vGuides.ToArray(),
                                        _draggedGuideIdx, _draggingHGuide,
                                        BuildShapeInfos());
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        RenderSkCore(canvas, snap, _bitmapCache);
        return bitmap;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public PreviewControl()
    {
        ClipToBounds = true;
        Focusable    = true;

        SelectedState.Self.SelectionChanged               += () => Dispatcher.UIThread.InvokeAsync(OnSelectionChanged);
        ApplicationEvents.Self.AnimationChainsChanged     += () => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
        ApplicationEvents.Self.AchxLoaded                += _ => Dispatcher.UIThread.InvokeAsync(OnSelectionChanged);
        AppCommands.Self.RefreshAnimationFrameDisplayRequested += () => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);

        _playback.FrameIndexChanged += _ => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    // ── Timer ─────────────────────────────────────────────────────────────────

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // Only advance when the whole chain is playing (no specific frame pinned)
        if (SelectedState.Self.SelectedFrame is not null) return;
        _playback.Advance(0.016);
    }

    // ── State reset ───────────────────────────────────────────────────────────

    private void OnSelectionChanged()
    {
        _playback.SetChain(SelectedState.Self.SelectedChain);
        InvalidateVisual();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired after every zoom change. Payload is the new zoom as a percentage
    /// (e.g. 100f = 100 %). Wheel-zoom can land on values that are not present
    /// in any preset combo box bound to this control; subscribers should be
    /// prepared to display arbitrary percentages.
    /// </summary>
    public event Action<float>? ZoomChanged;

    public void SetZoomPercent(int pct)
    {
        _zoom = Math.Clamp(pct / 100f, 0.05f, 32f);
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoom * 100f);
    }

    /// <summary>Current zoom factor (1.0 = 100 %).</summary>
    public float Zoom => _zoom;

    /// <summary>Test-only: adds a horizontal guide at the given world-Y coordinate.</summary>
    public void AddHGuide(float worldY) { _hGuides.Add(worldY); InvalidateVisual(); }

    /// <summary>Test-only: adds a vertical guide at the given world-X coordinate.</summary>
    public void AddVGuide(float worldX) { _vGuides.Add(worldX); InvalidateVisual(); }

    /// <summary>Test-only: returns the number of user-created horizontal guides.</summary>
    public int HGuideCount => _hGuides.Count;

    /// <summary>Test-only: returns the number of user-created vertical guides.</summary>
    public int VGuideCount => _vGuides.Count;

    /// <summary>
    /// Test-only: simulates a right-click at the given control-space point,
    /// removing any guide within hit distance. Mirrors <see cref="OnPointerPressed"/> so
    /// headless tests can drive the right-click removal code path without synthesising events.
    /// </summary>
    public void SimulateRightClick(float x, float y) => TryRemoveGuideAt(x, y);

    /// <summary>
    /// Test-only: simulates a single mouse-wheel zoom event toward the given
    /// control-space point. Mirrors <see cref="OnPointerWheelChanged"/> so
    /// headless tests can drive the same code path without synthesising
    /// pointer events.
    /// </summary>
    public void SimulateWheelZoom(double x, double y, bool zoomIn)
    {
        ApplyWheelZoom(x, y, zoomIn ? 1.25f : 0.8f);
    }

    private void ApplyWheelZoom(double x, double y, float factor)
    {
        float oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom * factor, 0.05f, 32f);
        float ratio = _zoom / oldZoom;
        float cx0 = (float)((Bounds.Width  - RulerSize) / 2f + RulerSize);
        float cy0 = (float)((Bounds.Height - RulerSize) / 2f + RulerSize);
        _panX = (float)((x - cx0) - (x - cx0 - _panX) * ratio);
        _panY = (float)((y - cy0) - (y - cy0 - _panY) * ratio);
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoom * 100f);
    }

    public void SetPan(float panX, float panY)
    {
        _panX = panX;
        _panY = panY;
        InvalidateVisual();
    }

    // ── Bitmap cache helpers ──────────────────────────────────────────────────

    private SKBitmap? GetBitmap(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_bitmapCache.TryGetValue(path, out var cached)) return cached;
        try
        {
            var bm = SKBitmap.Decode(path);
            _bitmapCache[path] = bm;
            return bm;
        }
        catch
        {
            _bitmapCache[path] = null;
            return null;
        }
    }

    private string? ResolveTexturePath(AnimationFrameSave? frame)
    {
        if (frame is null || string.IsNullOrEmpty(frame.TextureName)) return null;

        // Absolute path (e.g. drag-dropped textures before an ACHX file is saved).
        if (Path.IsPathRooted(frame.TextureName))
            return File.Exists(frame.TextureName) ? frame.TextureName : null;

        // Relative path: requires a saved ACHX to derive the base folder.
        if (string.IsNullOrEmpty(ProjectManager.Self.FileName))
            return null;
        string achxFolder = FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName);
        string full = new FilePath(achxFolder + frame.TextureName).FullPath;
        if (!File.Exists(full))
            return null;
        return full;
    }

    // ── Avalonia rendering ────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        var chain         = SelectedState.Self.SelectedChain;
        var selectedFrame = SelectedState.Self.SelectedFrame;

        AnimationFrameSave? displayFrame = null;
        AnimationFrameSave? onionFrame   = null;

        if (selectedFrame is not null)
        {
            displayFrame = selectedFrame;
            if (_showOnionSkin && chain is not null && chain.Frames.Count > 1)
            {
                int idx     = chain.Frames.IndexOf(selectedFrame);
                int prevIdx = (idx - 1 + chain.Frames.Count) % chain.Frames.Count;
                if (prevIdx != idx)
                    onionFrame = chain.Frames[prevIdx];
            }
        }
        else if (chain is not null && chain.Frames.Count > 0)
        {
            int idx  = Math.Clamp(_playback.CurrentFrameIndex, 0, chain.Frames.Count - 1);
            displayFrame = chain.Frames[idx];
        }

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // Pre-fill bitmap cache synchronously before handing off to render thread
        string? texPath   = ResolveTexturePath(displayFrame);
        string? onionPath = ResolveTexturePath(onionFrame);
        GetBitmap(texPath);
        GetBitmap(onionPath);

        ctx.Custom(new DrawOp(
            new RenderSnapshot(
                displayFrame, onionFrame, _zoom, _panX, _panY, _showGuides,
                texPath, onionPath, (float)w, (float)h,
                AppState.Self.OffsetMultiplier,
                _hGuides.ToArray(), _vGuides.ToArray(),
                _draggedGuideIdx, _draggingHGuide,
                BuildShapeInfos()),
            _bitmapCache));
    }

    // ── Guide helpers ─────────────────────────────────────────────────────────

    private float GetCenterX(float? width  = null) => ((width  ?? (float)Bounds.Width)  - RulerSize) / 2f + RulerSize + _panX;
    private float GetCenterY(float? height = null) => ((height ?? (float)Bounds.Height) - RulerSize) / 2f + RulerSize + _panY;
    private float WorldToScreenY(float wy) => GetCenterY() + wy * _zoom;
    private float WorldToScreenX(float wx) => GetCenterX() + wx * _zoom;
    private float ScreenToWorldY(float sy, float? height = null) => (sy - GetCenterY(height)) / _zoom;
    private float ScreenToWorldX(float sx, float? width  = null) => (sx - GetCenterX(width))  / _zoom;

    // Guides snap to the nearest integer (pixel boundary) in world space.
    private static float SnapToPixel(float world) => MathF.Round(world, MidpointRounding.AwayFromZero);

    // ── Test helpers (internal) ───────────────────────────────────────────────

    /// <summary>Horizontal guide world-Y values; exposed for headless tests.</summary>
    internal IReadOnlyList<float> HGuides => _hGuides;

    /// <summary>Vertical guide world-X values; exposed for headless tests.</summary>
    internal IReadOnlyList<float> VGuides => _vGuides;

    /// <summary>
    /// Simulates a ruler-click that creates a horizontal guide at <paramref name="screenY"/>
    /// within a control of height <paramref name="controlHeight"/>. Applies the same
    /// snap-to-pixel logic as the live pointer handler.
    /// </summary>
    internal void SimulateAddHGuide(float screenY, float controlHeight)
    {
        _hGuides.Add(SnapToPixel(ScreenToWorldY(screenY, controlHeight)));
        InvalidateVisual();
    }

    /// <summary>
    /// Simulates a ruler-click that creates a vertical guide at <paramref name="screenX"/>
    /// within a control of width <paramref name="controlWidth"/>. Applies the same
    /// snap-to-pixel logic as the live pointer handler.
    /// </summary>
    internal void SimulateAddVGuide(float screenX, float controlWidth)
    {
        _vGuides.Add(SnapToPixel(ScreenToWorldX(screenX, controlWidth)));
        InvalidateVisual();
    }

    /// <summary>
    /// Simulates dragging horizontal guide at <paramref name="idx"/> to
    /// <paramref name="screenY"/>. Applies snap-to-pixel.
    /// </summary>
    internal void SimulateDragHGuide(int idx, float screenY, float controlHeight)
    {
        _hGuides[idx] = SnapToPixel(ScreenToWorldY(screenY, controlHeight));
        InvalidateVisual();
    }

    /// <summary>
    /// Simulates dragging vertical guide at <paramref name="idx"/> to
    /// <paramref name="screenX"/>. Applies snap-to-pixel.
    /// </summary>
    internal void SimulateDragVGuide(int idx, float screenX, float controlWidth)
    {
        _vGuides[idx] = SnapToPixel(ScreenToWorldX(screenX, controlWidth));
        InvalidateVisual();
    }

    /// <summary>
    /// Marks guide <paramref name="idx"/> as the active drag target so the
    /// value label renders in <see cref="RenderToBitmap"/>. Use in headless
    /// tests together with <see cref="SimulateAddHGuide"/> /
    /// <see cref="SimulateAddVGuide"/> to verify label rendering.
    /// </summary>
    internal void SimulateBeginGuideDrag(bool isHorizontal, int idx)
    {
        _draggedGuideIdx = idx;
        _draggingHGuide  = isHorizontal;
        InvalidateVisual();
    }

    /// <summary>Clears the active drag state set by <see cref="SimulateBeginGuideDrag"/>.</summary>
    internal void SimulateEndGuideDrag()
    {
        _draggedGuideIdx = -1;
        InvalidateVisual();
    }

    /// <summary>
    /// Formats the coordinate label shown next to a guide while it is being dragged.
    /// Returns <c>"Y: N"</c> for horizontal guides and <c>"X: N"</c> for vertical ones.
    /// </summary>
    internal static string FormatGuideLabel(bool isHorizontal, float worldValue)
        => isHorizontal
            ? $"Y: {(int)MathF.Round(worldValue)}"
            : $"X: {(int)MathF.Round(worldValue)}";

    /// <summary>
    /// Returns the cursor type to show when the pointer is at screen position
    /// (<paramref name="px"/>, <paramref name="py"/>), based on proximity to
    /// user-placed guides. Returns <c>null</c> when no guide is nearby.
    /// </summary>
    public StandardCursorType? GetGuideCursorAt(float px, float py)
        => GuideCursorResolver.CursorTypeAt(px, py,
            _hGuides.ToArray(), _vGuides.ToArray(),
            _panX, _panY, _zoom,
            (float)Bounds.Width, (float)Bounds.Height);

    private void UpdateHoverCursor(Point pos)
    {
        StandardCursorType? cursorType = _draggedGuideIdx >= 0
            ? (_draggingHGuide ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast)
            : GetGuideCursorAt((float)pos.X, (float)pos.Y);
        Cursor = cursorType is null ? Cursor.Default : new Cursor(cursorType.Value);
    }

    /// <summary>
    /// Captures a thread-safe snapshot of collision shapes attached to the currently
    /// selected frame. Shapes are sourced from <see cref="SelectedState.Self.SelectedFrame"/>
    /// so they only appear when a specific frame is pinned (not during free playback).
    /// </summary>
    private PreviewShapeInfo[] BuildShapeInfos()
    {
        var frame = SelectedState.Self.SelectedFrame;
        if (frame?.ShapeCollectionSave is null) return Array.Empty<PreviewShapeInfo>();

        var selectedRects = SelectedState.Self.SelectedRectangles.ToHashSet();
        if (SelectedState.Self.SelectedRectangle is { } sr) selectedRects.Add(sr);

        var selectedCircles = SelectedState.Self.SelectedCircles.ToHashSet();
        if (SelectedState.Self.SelectedCircle is { } sc) selectedCircles.Add(sc);

        var list = new List<PreviewShapeInfo>();
        foreach (var r in frame.ShapeCollectionSave.AxisAlignedRectangleSaves)
            list.Add(new PreviewShapeInfo(PreviewShapeKind.Rect, r.X, r.Y, r.ScaleX, r.ScaleY,
                selectedRects.Contains(r)));
        foreach (var c in frame.ShapeCollectionSave.CircleSaves)
            list.Add(new PreviewShapeInfo(PreviewShapeKind.Circle, c.X, c.Y, c.Radius, 0f,
                selectedCircles.Contains(c)));
        return list.ToArray();
    }

    // ── Pointer events ────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the first guide within hit distance of (<paramref name="px"/>, <paramref name="py"/>).
    /// Clicks inside the ruler strips are ignored — guides are only visible in the canvas area.
    /// Returns <c>true</c> if a guide was removed.
    /// </summary>
    private bool TryRemoveGuideAt(float px, float py)
    {
        if (px < RulerSize || py < RulerSize) return false;

        const float hitPx = 4f;
        for (int i = 0; i < _hGuides.Count; i++)
        {
            if (MathF.Abs(py - WorldToScreenY(_hGuides[i])) < hitPx)
            {
                _hGuides.RemoveAt(i);
                InvalidateVisual();
                return true;
            }
        }
        for (int i = 0; i < _vGuides.Count; i++)
        {
            if (MathF.Abs(px - WorldToScreenX(_vGuides[i])) < hitPx)
            {
                _vGuides.RemoveAt(i);
                InvalidateVisual();
                return true;
            }
        }
        return false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        float factor = e.Delta.Y > 0 ? 1.25f : 0.8f;
        var pt = e.GetPosition(this);
        // Zoom toward the cursor: the world coordinate under pt must stay fixed.
        ApplyWheelZoom(pt.X, pt.Y, factor);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;
        var pos   = e.GetPosition(this);
        float px  = (float)pos.X;
        float py  = (float)pos.Y;

        if (props.IsMiddleButtonPressed ||
            (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _isPanning    = true;
            _lastMousePt  = pos;
            Cursor        = Cursor.Default;
            e.Pointer.Capture(this);
            return;
        }

        if (props.IsRightButtonPressed)
        {
            TryRemoveGuideAt(px, py);
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        // Click in left ruler strip → create horizontal guide
        if (px < RulerSize && py >= RulerSize)
        {
            float wy = SnapToPixel(ScreenToWorldY(py));
            _hGuides.Add(wy);
            _draggedGuideIdx = _hGuides.Count - 1;
            _draggingHGuide  = true;
            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }

        // Click in top ruler strip → create vertical guide
        if (py < RulerSize && px >= RulerSize)
        {
            float wx = SnapToPixel(ScreenToWorldX(px));
            _vGuides.Add(wx);
            _draggedGuideIdx = _vGuides.Count - 1;
            _draggingHGuide  = false;
            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }

        // Click near existing guide → drag it
        const float hitPx = 4f;
        for (int i = 0; i < _hGuides.Count; i++)
        {
            if (MathF.Abs(py - WorldToScreenY(_hGuides[i])) < hitPx)
            {
                _draggedGuideIdx = i;
                _draggingHGuide  = true;
                e.Pointer.Capture(this);
                return;
            }
        }
        for (int i = 0; i < _vGuides.Count; i++)
        {
            if (MathF.Abs(px - WorldToScreenX(_vGuides[i])) < hitPx)
            {
                _draggedGuideIdx = i;
                _draggingHGuide  = false;
                e.Pointer.Capture(this);
                return;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_isPanning)
        {
            _panX      += (float)(pos.X - _lastMousePt.X);
            _panY      += (float)(pos.Y - _lastMousePt.Y);
            _lastMousePt = pos;
            InvalidateVisual();
            return;
        }

        if (_draggedGuideIdx >= 0)
        {
            if (_draggingHGuide)
                _hGuides[_draggedGuideIdx] = SnapToPixel(ScreenToWorldY((float)pos.Y));
            else
                _vGuides[_draggedGuideIdx] = SnapToPixel(ScreenToWorldX((float)pos.X));
            InvalidateVisual();
        }

        UpdateHoverCursor(pos);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_draggedGuideIdx >= 0)
        {
            var pos = e.GetPosition(this);
            bool outside = pos.X < 0 || pos.Y < 0 || pos.X > Bounds.Width || pos.Y > Bounds.Height;
            if (outside)
            {
                if (_draggingHGuide)
                    _hGuides.RemoveAt(_draggedGuideIdx);
                else
                    _vGuides.RemoveAt(_draggedGuideIdx);
            }
            _draggedGuideIdx = -1;
            InvalidateVisual();
            UpdateHoverCursor(pos);
        }

        _isPanning = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Cursor = Cursor.Default;
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private enum PreviewShapeKind { Rect, Circle }

    /// <summary>
    /// Immutable snapshot of a single collision shape, safe to pass to the render thread.
    /// <para>For <see cref="PreviewShapeKind.Rect"/>: Param1=ScaleX, Param2=ScaleY.</para>
    /// <para>For <see cref="PreviewShapeKind.Circle"/>: Param1=Radius, Param2=0.</para>
    /// </summary>
    private record PreviewShapeInfo(
        PreviewShapeKind Kind,
        float X, float Y,
        float Param1, float Param2,
        bool IsSelected);

    private record RenderSnapshot(
        AnimationFrameSave? Frame,
        AnimationFrameSave? OnionFrame,
        float  Zoom,
        float  PanX, float PanY,
        bool   ShowGuides,
        string? TexturePath,
        string? OnionTexturePath,
        float  Width, float Height,
        float  OffsetMultiplier,
        float[] HGuides,
        float[] VGuides,
        int    DraggedGuideIdx,
        bool   DraggingHGuide,
        PreviewShapeInfo[] Shapes);

    // ── Shared SkiaSharp rendering (used by both live and off-screen paths) ───

    private static void RenderSkCore(
        SKCanvas canvas, RenderSnapshot s, Dictionary<string, SKBitmap?> cache)
    {
        canvas.Clear(new SKColor(30, 30, 30));

        // Content origin is shifted so the ruler strips sit at the left/top edges
        float cx = (s.Width  - RulerSize) / 2f + RulerSize + s.PanX;
        float cy = (s.Height - RulerSize) / 2f + RulerSize + s.PanY;

        // ── Clip content/guide drawing to the non-ruler area ─────────────────
        canvas.Save();
        canvas.ClipRect(new SKRect(RulerSize, RulerSize, s.Width, s.Height));

        if (s.OnionFrame is not null &&
            s.OnionTexturePath is not null &&
            cache.TryGetValue(s.OnionTexturePath, out var onionBm) && onionBm is not null)
        {
            float ocx = cx + s.OnionFrame.RelativeX * s.OffsetMultiplier * s.Zoom;
            float ocy = cy - s.OnionFrame.RelativeY * s.OffsetMultiplier * s.Zoom;
            DrawFrameCore(canvas, s.OnionFrame, onionBm, ocx, ocy, s.Zoom, alpha: 0.4f);
        }

        if (s.Frame is not null &&
            s.TexturePath is not null &&
            cache.TryGetValue(s.TexturePath, out var bm) && bm is not null)
        {
            float fcx = cx + s.Frame.RelativeX * s.OffsetMultiplier * s.Zoom;
            float fcy = cy - s.Frame.RelativeY * s.OffsetMultiplier * s.Zoom;
            DrawFrameCore(canvas, s.Frame, bm, fcx, fcy, s.Zoom, alpha: 1.0f);
        }

        // Origin crosshair (toggled by ShowGuides)
        if (s.ShowGuides)
        {
            using var gp = new SKPaint
            {
                Color       = new SKColor(100, 200, 100, 160),
                StrokeWidth = 1f,
                IsAntialias = false
            };
            canvas.DrawLine(cx, RulerSize, cx,       s.Height, gp);
            canvas.DrawLine(RulerSize, cy, s.Width,  cy,       gp);
        }

        // User-created guide lines
        if (s.HGuides.Length > 0 || s.VGuides.Length > 0)
        {
            using var guidePaint = new SKPaint
            {
                Color       = new SKColor(0, 200, 255, 200),
                StrokeWidth = 1f,
                IsAntialias = false
            };
            foreach (float wy in s.HGuides)
            {
                float sy = cy + wy * s.Zoom;
                if (sy >= RulerSize && sy <= s.Height)
                    canvas.DrawLine(RulerSize, sy, s.Width, sy, guidePaint);
            }
            foreach (float wx in s.VGuides)
            {
                float sx = cx + wx * s.Zoom;
                if (sx >= RulerSize && sx <= s.Width)
                    canvas.DrawLine(sx, RulerSize, sx, s.Height, guidePaint);
            }
        }

        // ── Collision shapes (AxisAlignedRectangles and Circles) ─────────────
        if (s.Shapes.Length > 0)
        {
            float om = s.OffsetMultiplier * s.Zoom;
            using var shapePaint   = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var selectedPaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2f,   IsAntialias = true };

            foreach (var sh in s.Shapes)
            {
                var paint = sh.IsSelected ? selectedPaint : shapePaint;
                paint.Color = sh.IsSelected
                    ? new SKColor(255, 220, 0, 230)   // gold for selected
                    : new SKColor(0,   230, 80,  200); // green for unselected

                // Shape coords: X right, Y up (FRB convention); negate Y for screen
                float sx = cx + sh.X * om;
                float sy = cy - sh.Y * om;

                if (sh.Kind == PreviewShapeKind.Rect)
                {
                    float hw = sh.Param1 * om;
                    float hh = sh.Param2 * om;
                    canvas.DrawRect(new SKRect(sx - hw, sy - hh, sx + hw, sy + hh), paint);
                }
                else
                {
                    canvas.DrawCircle(sx, sy, sh.Param1 * om, paint);
                }
            }
        }

        // Dragged guide value label — drawn last so it stays on top of shapes
        if (s.DraggedGuideIdx >= 0)
        {
            using var dragLabelPaint = new SKPaint
            {
                Color       = new SKColor(0, 200, 255, 230),
                TextSize    = 11f,
                IsAntialias = true
            };
            const float lMargin = 4f;
            const float lHeight = 13f; // approximate text height at TextSize=11

            if (s.DraggingHGuide && s.DraggedGuideIdx < s.HGuides.Length)
            {
                float wy = s.HGuides[s.DraggedGuideIdx];
                float sy = cy + wy * s.Zoom;
                if (sy >= RulerSize && sy <= s.Height)
                {
                    float baseline = Math.Clamp(sy - 2f, RulerSize + lHeight, s.Height - lMargin);
                    canvas.DrawText(FormatGuideLabel(true, wy), RulerSize + lMargin, baseline, dragLabelPaint);
                }
            }
            else if (!s.DraggingHGuide && s.DraggedGuideIdx < s.VGuides.Length)
            {
                float wx = s.VGuides[s.DraggedGuideIdx];
                float sx = cx + wx * s.Zoom;
                if (sx >= RulerSize && sx <= s.Width)
                {
                    float lx = Math.Clamp(sx + lMargin, RulerSize + lMargin, s.Width - 50f);
                    canvas.DrawText(FormatGuideLabel(false, wx), lx, RulerSize + lHeight, dragLabelPaint);
                }
            }
        }

        canvas.Restore(); // end content clip

        // ── Ruler strips ─────────────────────────────────────────────────────
        using var rulerBg = new SKPaint { Color = new SKColor(50, 50, 55) };
        canvas.DrawRect(new SKRect(0,         0, s.Width, RulerSize), rulerBg);   // top
        canvas.DrawRect(new SKRect(0, RulerSize, RulerSize, s.Height), rulerBg);  // left
        canvas.DrawRect(new SKRect(0,         0, RulerSize, RulerSize), rulerBg); // corner

        using var tickPaint = new SKPaint
        {
            Color       = new SKColor(160, 160, 165),
            StrokeWidth = 1f,
            IsAntialias = false
        };
        using var labelPaint = new SKPaint
        {
            Color    = new SKColor(190, 190, 195),
            TextSize = 8f,
            IsAntialias = true
        };

        float majorStep = GetRulerStep(s.Zoom);
        float minorStep = majorStep / 5f;

        // Top (horizontal) ruler — ticks at world-X positions
        float wxStart = (RulerSize - cx) / s.Zoom;
        float wxEnd   = (s.Width  - cx) / s.Zoom;
        for (float wx = MathF.Floor(wxStart / minorStep) * minorStep; wx <= wxEnd; wx += minorStep)
        {
            float sx = cx + wx * s.Zoom;
            if (sx < RulerSize || sx > s.Width) continue;
            bool isMajor = MathF.Abs(wx % majorStep) < minorStep * 0.4f;
            float tickH = isMajor ? RulerSize * 0.55f : RulerSize * 0.30f;
            canvas.DrawLine(sx, RulerSize - tickH, sx, RulerSize, tickPaint);
            if (isMajor)
                canvas.DrawText(((int)MathF.Round(wx)).ToString(), sx + 1f, RulerSize - tickH - 1f, labelPaint);
        }

        // Left (vertical) ruler — ticks at world-Y positions
        float wyStart = (RulerSize - cy) / s.Zoom;
        float wyEnd   = (s.Height  - cy) / s.Zoom;
        for (float wy = MathF.Floor(wyStart / minorStep) * minorStep; wy <= wyEnd; wy += minorStep)
        {
            float sy = cy + wy * s.Zoom;
            if (sy < RulerSize || sy > s.Height) continue;
            bool isMajor = MathF.Abs(wy % majorStep) < minorStep * 0.4f;
            float tickW = isMajor ? RulerSize * 0.55f : RulerSize * 0.30f;
            canvas.DrawLine(RulerSize - tickW, sy, RulerSize, sy, tickPaint);
            if (isMajor)
            {
                canvas.Save();
                canvas.Translate(RulerSize - tickW - 1f, sy);
                canvas.RotateDegrees(-90f);
                canvas.DrawText(((int)MathF.Round(wy)).ToString(), 0f, 0f, labelPaint);
                canvas.Restore();
            }
        }

        // Draw guide value labels on the ruler edge
        using var guideTickPaint = new SKPaint
        {
            Color       = new SKColor(0, 200, 255, 200),
            StrokeWidth = 1f,
            IsAntialias = false
        };
        foreach (float wy in s.HGuides)
        {
            float sy = cy + wy * s.Zoom;
            if (sy >= RulerSize && sy <= s.Height)
                canvas.DrawLine(0, sy, RulerSize, sy, guideTickPaint);
        }
        foreach (float wx in s.VGuides)
        {
            float sx = cx + wx * s.Zoom;
            if (sx >= RulerSize && sx <= s.Width)
                canvas.DrawLine(sx, 0, sx, RulerSize, guideTickPaint);
        }

        // Ruler border lines
        using var borderPaint = new SKPaint { Color = new SKColor(80, 80, 85), StrokeWidth = 1f };
        canvas.DrawLine(RulerSize, 0, RulerSize, s.Height, borderPaint);
        canvas.DrawLine(0, RulerSize, s.Width, RulerSize, borderPaint);
    }

    private static float GetRulerStep(float zoom)
    {
        float targetWorld = 50f / zoom; // target ~50 screen px per major tick
        float[] candidates = { 1, 2, 5, 10, 25, 50, 100, 250, 500, 1000 };
        foreach (float c in candidates)
            if (c >= targetWorld) return c;
        return 1000f;
    }

    private static void DrawFrameCore(
        SKCanvas canvas, AnimationFrameSave frame, SKBitmap bm,
        float cx, float cy, float zoom, float alpha)
    {
        int tw = bm.Width, th = bm.Height;
        int sx = (int)(frame.LeftCoordinate   * tw);
        int sy = (int)(frame.TopCoordinate    * th);
        int sw = (int)Math.Max(1, (frame.RightCoordinate  - frame.LeftCoordinate)  * tw);
        int sh = (int)Math.Max(1, (frame.BottomCoordinate - frame.TopCoordinate)   * th);

        var src = SKRectI.Create(sx, sy, sw, sh);
        float dw = sw * zoom;
        float dh = sh * zoom;
        float dx = cx - dw / 2;
        float dy = cy - dh / 2;
        var dst = SKRect.Create(dx, dy, dw, dh);

        using var paint = new SKPaint
        {
            FilterQuality = zoom >= 1 ? SKFilterQuality.None : SKFilterQuality.Low,
            Color         = new SKColor(255, 255, 255, (byte)(255 * alpha))
        };

        bool flip = FlipScaleCalculator.IsFlipped(frame.FlipHorizontal, frame.FlipVertical);
        if (flip)
        {
            canvas.Save();
            var (scaleX, scaleY) = FlipScaleCalculator.Compute(frame.FlipHorizontal, frame.FlipVertical);
            canvas.Scale(scaleX, scaleY, cx, cy);
        }

        canvas.DrawBitmap(bm, src, dst, paint);

        if (flip) canvas.Restore();

        using var op = new SKPaint
        {
            Color       = new SKColor(255, 255, 255, (byte)(200 * alpha)),
            StrokeWidth = 1f,
            IsStroke    = true
        };
        canvas.DrawRect(dst, op);
    }

    private sealed class DrawOp : ICustomDrawOperation
    {
        private readonly RenderSnapshot              _snap;
        private readonly Dictionary<string, SKBitmap?> _cache;

        public DrawOp(RenderSnapshot snap, Dictionary<string, SKBitmap?> cache)
        {
            _snap  = snap;
            _cache = cache;
        }

        public Rect Bounds => new(0, 0, _snap.Width, _snap.Height);
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => true;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext ctx)
        {
            var feature = ctx.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;
            using var lease = feature.Lease();
            PreviewControl.RenderSkCore(lease.SkCanvas, _snap, _cache);
        }
    }
}
