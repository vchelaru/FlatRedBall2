using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Animated sprite preview panel. Plays the selected AnimationChain at runtime speed
/// (one frame = FrameLength seconds). When a single frame is selected, shows that
/// frame statically with optional onion-skin overlay.
/// </summary>
public class PreviewControl : Control
{
    // -- Animation state -------------------------------------------------------
    private readonly DispatcherTimer _timer;
    private readonly AnimationEditor.Core.CommandsAndState.PlaybackController _playback = new();

    // -- Bitmap cache ----------------------------------------------------------
    private readonly Dictionary<string, SKBitmap?> _bitmapCache =
        new(StringComparer.OrdinalIgnoreCase);

    // -- Camera ----------------------------------------------------------------
    private float _zoom = 1f;
    private float _panX, _panY;

    // -- Settings --------------------------------------------------------------
    private bool _showOnionSkin;
    private bool _showGuides;

    // -- Pan drag --------------------------------------------------------------
    private bool  _isPanning;
    private Point _lastMousePt;

    // -- Rulers / guides -------------------------------------------------------
    private const float RulerSize = 20f;
    private readonly List<float> _hGuides = new(); // world-Y values (positive = down on screen)
    private readonly List<float> _vGuides = new(); // world-X values (positive = right on screen)
    private int  _draggedGuideIdx = -1;
    private bool _draggingHGuide;                  // true = horizontal guide

    // -- Shape drag -----------------------------------------------------------
    private object?    _draggingShape;   // AARectSave or CircleSave
    private float      _shapeDragStartX;
    private float      _shapeDragStartY;
    private Point      _shapeDragAnchor;
    private HandleKind _shapeResizeHandle  = HandleKind.None;
    private float      _shapeDragStartScaleX;  // rect ScaleX or circle Radius at drag start
    private float      _shapeDragStartScaleY;  // rect ScaleY at drag start (0 for circle)

    // -- Public properties -----------------------------------------------------

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
        var chain         = _selectedState!.SelectedChain;
        var selectedFrame = _selectedState!.SelectedFrame;

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
                                        _appState!.OffsetMultiplier,
                                        _hGuides.ToArray(), _vGuides.ToArray(),
                                        _draggedGuideIdx, _draggingHGuide,
                                        BuildShapeInfos());
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        RenderSkCore(canvas, snap, _bitmapCache);
        return bitmap;
    }

    // -- Injected services -----------------------------------------------------

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
        _selectedState  = selectedState;
        _appState       = appState;
        _appCommands    = appCommands;
        _events         = events;
        _projectManager = projectManager;
        _undoManager    = undoManager;

        _selectedState.SelectionChanged                        += () => Dispatcher.UIThread.InvokeAsync(OnSelectionChanged);
        _events.AnimationChainsChanged                         += () => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
        _events.AchxLoaded                                     += _ => Dispatcher.UIThread.InvokeAsync(OnSelectionChanged);
        _appCommands.RefreshAnimationFrameDisplayRequested     += () => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
    }

    // -- Constructor -----------------------------------------------------------

    public PreviewControl()
    {
        ClipToBounds = true;
        Focusable    = true;

        // Subscriptions are deferred to InitializeServices (called from MainWindow)

        _playback.FrameIndexChanged += _ => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    // -- Timer -----------------------------------------------------------------

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // Only advance when the whole chain is playing (no specific frame pinned)
        if (_selectedState!.SelectedFrame is not null) return;
        _playback.Advance(0.016);
    }

    // -- State reset -----------------------------------------------------------

    private void OnSelectionChanged()
    {
        _playback.SetChain(_selectedState!.SelectedChain);
        InvalidateVisual();
    }

    // -- Public API ------------------------------------------------------------

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

    /// <summary>Current pan offset (panX, panY).</summary>
    public (float X, float Y) PanOffset => (_panX, _panY);

    /// <summary>
    /// Pans the preview so the entity-space point (<paramref name="entityX"/>,
    /// <paramref name="entityY"/>) appears at the canvas centre.
    /// The current zoom and <see cref="IAppState.OffsetMultiplier"/> are preserved.
    /// </summary>
    public void CenterOnEntityPoint(float entityX, float entityY)
    {
        float offMult = _appState!.OffsetMultiplier;
        _panX = -entityX * offMult * _zoom;
        _panY =  entityY * offMult * _zoom;
        InvalidateVisual();
    }

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
    /// Test-only: simulates a left-click on the canvas at (<paramref name="px"/>, <paramref name="py"/>)
    /// and selects the topmost collision shape under the cursor, if any.
    /// Mirrors the shape-selection code path in <see cref="OnPointerPressed"/>.
    /// </summary>
    internal void SimulateCanvasClick(float px, float py) => TrySelectShapeAt(px, py);
    /// <summary>
    /// Test-only: applies a world-space drag delta to the currently selected shape and commits.
    /// Bypasses coordinate conversion, so results are independent of zoom/pan/OffsetMultiplier.
    /// </summary>
    internal void SimulateShapeDrag(float worldDx, float worldDy)
    {
        _draggingShape = _selectedState!.SelectedShape;
        if (_draggingShape is null) return;

        if (_draggingShape is AARectSave r)
        {
            _shapeDragStartX = r.X;
            _shapeDragStartY = r.Y;
            r.X += worldDx;
            r.Y += worldDy;
        }
        else if (_draggingShape is CircleSave c)
        {
            _shapeDragStartX = c.X;
            _shapeDragStartY = c.Y;
            c.X += worldDx;
            c.Y += worldDy;
        }
        CommitShapeDrag();
    }

    /// <summary>
    /// Test-only: applies new shape dimensions to the currently selected shape and commits a resize.
    /// Bypasses coordinate conversion, so results are independent of zoom/pan/OffsetMultiplier.
    /// For rectangles: <paramref name="newParam1"/> = ScaleX, <paramref name="newParam2"/> = ScaleY.
    /// For circles:    <paramref name="newParam1"/> = Radius,  <paramref name="newParam2"/> is ignored.
    /// </summary>
    internal void SimulateShapeResize(HandleKind handle, float newParam1, float newParam2 = 0f)
    {
        _draggingShape = _selectedState!.SelectedShape;
        if (_draggingShape is null) return;

        _shapeResizeHandle = handle;

        if (_draggingShape is AARectSave r)
        {
            _shapeDragStartX      = r.X;
            _shapeDragStartY      = r.Y;
            _shapeDragStartScaleX = r.ScaleX;
            _shapeDragStartScaleY = r.ScaleY;
            r.ScaleX = newParam1;
            r.ScaleY = newParam2;
        }
        else if (_draggingShape is CircleSave c)
        {
            _shapeDragStartX      = c.X;
            _shapeDragStartY      = c.Y;
            _shapeDragStartScaleX = c.Radius;
            _shapeDragStartScaleY = 0f;
            c.Radius = newParam1;
        }

        CommitShapeResize();
    }
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

    // -- Bitmap cache helpers --------------------------------------------------

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
        if (string.IsNullOrEmpty(_projectManager!.FileName))
            return null;
        string achxFolder = (Path.GetDirectoryName(_projectManager!.FileName) ?? string.Empty);
        string full = new FilePath(Path.Combine(achxFolder, frame.TextureName)).FullPath;
        if (!File.Exists(full))
            return null;
        return full;
    }

    // -- Avalonia rendering ----------------------------------------------------

    public override void Render(DrawingContext ctx)
    {
        var chain         = _selectedState!.SelectedChain;
        var selectedFrame = _selectedState!.SelectedFrame;

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
                _appState!.OffsetMultiplier,
                _hGuides.ToArray(), _vGuides.ToArray(),
                _draggedGuideIdx, _draggingHGuide,
                BuildShapeInfos()),
            _bitmapCache));
    }

    // -- Guide helpers ---------------------------------------------------------

    private float GetCenterX(float? width  = null) => ((width  ?? (float)Bounds.Width)  - RulerSize) / 2f + RulerSize + _panX;
    private float GetCenterY(float? height = null) => ((height ?? (float)Bounds.Height) - RulerSize) / 2f + RulerSize + _panY;
    private float WorldToScreenY(float wy) => GetCenterY() + wy * _zoom;
    private float WorldToScreenX(float wx) => GetCenterX() + wx * _zoom;
    private float ScreenToWorldY(float sy, float? height = null) => (sy - GetCenterY(height)) / _zoom;
    private float ScreenToWorldX(float sx, float? width  = null) => (sx - GetCenterX(width))  / _zoom;

    // Guides snap to the nearest integer (pixel boundary) in world space.
    private static float SnapToPixel(float world) => MathF.Round(world, MidpointRounding.AwayFromZero);

    // -- Test helpers (internal) ----------------------------------------------

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
        if (_draggingShape is not null)
        {
            Cursor = new Cursor(_shapeResizeHandle != HandleKind.None
                ? GetResizeCursor(_shapeResizeHandle)
                : StandardCursorType.SizeAll);
            return;
        }
        var handle = HitTestShapeHandle((float)pos.X, (float)pos.Y);
        if (handle != HandleKind.None)
        {
            Cursor = new Cursor(GetResizeCursor(handle));
            return;
        }
        StandardCursorType? cursorType = _draggedGuideIdx >= 0
            ? (_draggingHGuide ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast)
            : GetGuideCursorAt((float)pos.X, (float)pos.Y);
        if (cursorType is null && HitTestShape((float)pos.X, (float)pos.Y) is not null)
            cursorType = StandardCursorType.SizeAll;
        Cursor = cursorType is null ? Cursor.Default : new Cursor(cursorType.Value);
    }

    private static StandardCursorType GetResizeCursor(HandleKind kind) => kind switch
    {
        HandleKind.TopCenter or HandleKind.BotCenter => StandardCursorType.SizeNorthSouth,
        HandleKind.MidLeft   or HandleKind.MidRight  => StandardCursorType.SizeWestEast,
        HandleKind.TopLeft   or HandleKind.BotRight  => StandardCursorType.TopLeftCorner,
        HandleKind.TopRight  or HandleKind.BotLeft   => StandardCursorType.TopRightCorner,
        _ => StandardCursorType.SizeAll,
    };

    /// <summary>
    /// Captures a thread-safe snapshot of collision shapes attached to the currently
    /// selected frame. Shapes are sourced from <see cref="ISelectedState.SelectedFrame"/>
    /// so they only appear when a specific frame is pinned (not during free playback).
    /// </summary>
    private PreviewShapeInfo[] BuildShapeInfos()
    {
        var frame = _selectedState!.SelectedFrame;
        if (frame?.ShapesSave is null) return Array.Empty<PreviewShapeInfo>();

        var selectedRects = _selectedState!.SelectedRectangles.ToHashSet();
        if (_selectedState!.SelectedRectangle is { } sr) selectedRects.Add(sr);

        var selectedCircles = _selectedState!.SelectedCircles.ToHashSet();
        if (_selectedState!.SelectedCircle is { } sc) selectedCircles.Add(sc);

        var list = new List<PreviewShapeInfo>();
        foreach (var r in frame.ShapesSave!.AARectSaves)
            list.Add(new PreviewShapeInfo(PreviewShapeKind.Rect, r.X, r.Y, r.ScaleX, r.ScaleY,
                selectedRects.Contains(r)));
        foreach (var c in frame.ShapesSave!.CircleSaves)
            list.Add(new PreviewShapeInfo(PreviewShapeKind.Circle, c.X, c.Y, c.Radius, 0f,
                selectedCircles.Contains(c)));
        return list.ToArray();
    }

    // -- Pointer events --------------------------------------------------------

    /// <summary>
    /// Removes the first guide within hit distance of (<paramref name="px"/>, <paramref name="py"/>).
    /// Clicks inside the ruler strips are ignored - guides are only visible in the canvas area.
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

    /// <summary>
    /// Returns the topmost collision shape under the given screen-space point, or
    /// <c>null</c> if no shape is hit. The currently selected shape has priority over
    /// unselected shapes; circles are checked before rectangles (rendered on top).
    /// Returns <c>null</c> for clicks inside the ruler strips.
    /// </summary>
    private object? HitTestShape(float px, float py)
    {
        var frame = _selectedState!.SelectedFrame;
        if (frame?.ShapesSave is null) return null;
        if (px < RulerSize || py < RulerSize) return null;

        float cx = GetCenterX();
        float cy = GetCenterY();
        float om = _appState!.OffsetMultiplier * _zoom;
        const float tolerance = 5f;

        // Selected shape has priority so the user can drag what they already selected.
        var sel = _selectedState!.SelectedShape;
        if (sel is CircleSave selC)
        {
            float sx = cx + selC.X * om;
            float sy = cy - selC.Y * om;
            if (PreviewShapeHitTester.HitsCircle(px, py, sx, sy, selC.Radius * om, tolerance))
                return selC;
        }
        else if (sel is AARectSave selR)
        {
            float sx = cx + selR.X * om;
            float sy = cy - selR.Y * om;
            if (PreviewShapeHitTester.HitsRect(px, py, sx, sy, selR.ScaleX * om, selR.ScaleY * om, tolerance))
                return selR;
        }

        var circles = frame.ShapesSave!.CircleSaves;
        for (int i = circles.Count - 1; i >= 0; i--)
        {
            var c = circles[i];
            if (ReferenceEquals(c, sel)) continue;
            float sx = cx + c.X * om;
            float sy = cy - c.Y * om;
            if (PreviewShapeHitTester.HitsCircle(px, py, sx, sy, c.Radius * om, tolerance))
                return c;
        }

        var rects = frame.ShapesSave!.AARectSaves;
        for (int i = rects.Count - 1; i >= 0; i--)
        {
            var r = rects[i];
            if (ReferenceEquals(r, sel)) continue;
            float sx = cx + r.X * om;
            float sy = cy - r.Y * om;
            if (PreviewShapeHitTester.HitsRect(px, py, sx, sy, r.ScaleX * om, r.ScaleY * om, tolerance))
                return r;
        }

        return null;
    }

    /// <summary>
    /// Finalises an in-progress shape drag: records an undo command (unless the delta is
    /// negligible), fires <see cref="ApplicationEvents.RaiseAnimationChainsChanged"/>,
    /// saves, and re-assigns the selection so the property panel refreshes.
    /// </summary>
    private void CommitShapeDrag()
    {
        if (_draggingShape is null) return;

        float newX, newY;
        if (_draggingShape is AARectSave r)      { newX = r.X; newY = r.Y; }
        else { var c = (CircleSave)_draggingShape; newX = c.X; newY = c.Y; }

        const float eps = 1e-4f;
        if (MathF.Abs(newX - _shapeDragStartX) > eps || MathF.Abs(newY - _shapeDragStartY) > eps)
        {
            var frame = _selectedState!.SelectedFrame;
            if (frame is not null)
                _undoManager!.Record(new MoveShapeCommand(
                    frame, _draggingShape, _shapeDragStartX, _shapeDragStartY, newX, newY,
                    _appCommands!, _events!));
            _events!.RaiseAnimationChainsChanged();
            _appCommands!.SaveCurrentAnimationChainList();
        }

        // Re-assign to fire SelectionChanged so the property panel refreshes.
        if (_draggingShape is AARectSave rs) _selectedState!.SelectedRectangle = rs;
        else if (_draggingShape is CircleSave cs)          _selectedState!.SelectedCircle    = cs;

        _draggingShape = null;
    }

    /// <summary>
    /// Returns the resize <see cref="HandleKind"/> under the cursor for the currently selected
    /// shape, or <see cref="HandleKind.None"/> if no resize handle is hit.
    /// Handles are positioned outside the bounding box by <c>Hs</c> pixels.
    /// </summary>
    private HandleKind HitTestShapeHandle(float px, float py)
    {
        var sel = _selectedState!.SelectedShape;
        if (sel is null || _selectedState!.SelectedFrame is null) return HandleKind.None;
        if (px < RulerSize || py < RulerSize) return HandleKind.None;

        float cx = GetCenterX();
        float cy = GetCenterY();
        float om = _appState!.OffsetMultiplier * _zoom;
        const float Hs = 5f;

        float left, top, right, bottom;
        if (sel is AARectSave r)
        {
            float sx = cx + r.X * om;
            float sy = cy - r.Y * om;
            float hw = r.ScaleX * om;
            float hh = r.ScaleY * om;
            left = sx - hw; right  = sx + hw;
            top  = sy - hh; bottom = sy + hh;
        }
        else if (sel is CircleSave c)
        {
            float sx = cx + c.X * om;
            float sy = cy - c.Y * om;
            float sr = c.Radius * om;
            left = sx - sr; right  = sx + sr;
            top  = sy - sr; bottom = sy + sr;
        }
        else return HandleKind.None;

        var kind = DragHandleHitTester.GetHandleAt(px, py, left, top, right, bottom,
            hitRadius: Hs, handleOffset: Hs);
        // Only return resize handles; body clicks are handled by the existing HitTestShape path.
        return kind is HandleKind.None or HandleKind.Move ? HandleKind.None : kind;
    }

    /// <summary>
    /// Applies the in-progress shape resize based on the current screen-space pointer position.
    /// Called from <see cref="OnPointerMoved"/> while <see cref="_shapeResizeHandle"/> is active.
    /// </summary>
    private void ApplyShapeResize(Point pos)
    {
        if (_draggingShape is null || _shapeResizeHandle == HandleKind.None) return;

        float om = _appState!.OffsetMultiplier * _zoom;
        float worldDX = ((float)(pos.X - _shapeDragAnchor.X)) / om;
        float worldDY = -((float)(pos.Y - _shapeDragAnchor.Y)) / om; // Y flip: screen↓ = world↓

        if (_draggingShape is CircleSave circle)
        {
            // Use signed delta projected onto the handle's outward direction.
            float delta = _shapeResizeHandle switch
            {
                HandleKind.MidRight   =>  worldDX,
                HandleKind.MidLeft    => -worldDX,
                HandleKind.TopCenter  =>  worldDY,
                HandleKind.BotCenter  => -worldDY,
                HandleKind.TopRight   => (worldDX + worldDY) / 2f,
                HandleKind.TopLeft    => (-worldDX + worldDY) / 2f,
                HandleKind.BotRight   => (worldDX - worldDY) / 2f,
                HandleKind.BotLeft    => (-worldDX - worldDY) / 2f,
                _ => 0f,
            };
            circle.Radius = MathF.Max(0.5f, _shapeDragStartScaleX + delta);
        }
        else if (_draggingShape is AARectSave r)
        {
            float startLeft   = _shapeDragStartX - _shapeDragStartScaleX;
            float startRight  = _shapeDragStartX + _shapeDragStartScaleX;
            float startTop    = _shapeDragStartY + _shapeDragStartScaleY; // world Y up
            float startBottom = _shapeDragStartY - _shapeDragStartScaleY;

            float newLeft   = startLeft,  newRight  = startRight;
            float newTop    = startTop,   newBottom = startBottom;

            switch (_shapeResizeHandle)
            {
                case HandleKind.MidRight:   newRight  = startRight  + worldDX; break;
                case HandleKind.MidLeft:    newLeft   = startLeft   + worldDX; break;
                case HandleKind.TopCenter:  newTop    = startTop    + worldDY; break;
                case HandleKind.BotCenter:  newBottom = startBottom + worldDY; break;
                case HandleKind.TopRight:   newRight  = startRight  + worldDX; newTop    = startTop    + worldDY; break;
                case HandleKind.TopLeft:    newLeft   = startLeft   + worldDX; newTop    = startTop    + worldDY; break;
                case HandleKind.BotRight:   newRight  = startRight  + worldDX; newBottom = startBottom + worldDY; break;
                case HandleKind.BotLeft:    newLeft   = startLeft   + worldDX; newBottom = startBottom + worldDY; break;
            }

            const float minSize = 1f; // minimum 1 world unit on each axis
            if (newRight - newLeft < minSize)
            {
                if (_shapeResizeHandle is HandleKind.MidLeft or HandleKind.TopLeft or HandleKind.BotLeft)
                    newLeft = newRight - minSize;
                else
                    newRight = newLeft + minSize;
            }
            if (newTop - newBottom < minSize)
            {
                if (_shapeResizeHandle is HandleKind.TopCenter or HandleKind.TopRight or HandleKind.TopLeft)
                    newTop = newBottom + minSize;
                else
                    newBottom = newTop - minSize;
            }

            r.X      = (newLeft   + newRight)  / 2f;
            r.Y      = (newTop    + newBottom)  / 2f;
            r.ScaleX = (newRight  - newLeft)    / 2f;
            r.ScaleY = (newTop    - newBottom)  / 2f;
        }
    }

    /// <summary>
    /// Finalises an in-progress shape resize: records an undo command (unless the delta is
    /// negligible), fires <see cref="ApplicationEvents.RaiseAnimationChainsChanged"/>,
    /// saves, and re-assigns the selection so the property panel refreshes.
    /// </summary>
    private void CommitShapeResize()
    {
        if (_draggingShape is null || _shapeResizeHandle == HandleKind.None) return;

        float newX, newY, newP1, newP2;
        if (_draggingShape is AARectSave r)
            { newX = r.X; newY = r.Y; newP1 = r.ScaleX; newP2 = r.ScaleY; }
        else
            { var c = (CircleSave)_draggingShape; newX = c.X; newY = c.Y; newP1 = c.Radius; newP2 = 0f; }

        const float eps = 1e-4f;
        bool changed = MathF.Abs(newX  - _shapeDragStartX)      > eps
                    || MathF.Abs(newY  - _shapeDragStartY)       > eps
                    || MathF.Abs(newP1 - _shapeDragStartScaleX)  > eps
                    || MathF.Abs(newP2 - _shapeDragStartScaleY)  > eps;

        if (changed)
        {
            var frame = _selectedState!.SelectedFrame;
            if (frame is not null)
                _undoManager!.Record(new ResizeShapeCommand(
                    frame, _draggingShape,
                    _shapeDragStartX, _shapeDragStartY, _shapeDragStartScaleX, _shapeDragStartScaleY,
                    newX, newY, newP1, newP2,
                    _appCommands!, _events!));
            _events!.RaiseAnimationChainsChanged();
            _appCommands!.SaveCurrentAnimationChainList();
        }

        if (_draggingShape is AARectSave rs) _selectedState!.SelectedRectangle = rs;
        else if (_draggingShape is CircleSave cs)          _selectedState!.SelectedCircle    = cs;

        _shapeResizeHandle = HandleKind.None;
        _draggingShape     = null;
    }

    /// <summary>
    /// Selects the topmost collision shape under (<paramref name="px"/>, <paramref name="py"/>)
    /// in screen space. Circles are checked before rectangles because they are rendered on top.
    /// Within each type, shapes are iterated in reverse render order so the last-drawn (topmost)
    /// shape wins when two shapes overlap.
    /// Returns <c>true</c> if a shape was selected.
    /// </summary>
    private bool TrySelectShapeAt(float px, float py)
    {
        var frame = _selectedState!.SelectedFrame;
        if (frame?.ShapesSave is null) return false;
        if (px < RulerSize || py < RulerSize) return false;

        float cx = GetCenterX();
        float cy = GetCenterY();
        float om = _appState!.OffsetMultiplier * _zoom;
        const float tolerance = 5f;

        // Circles are rendered after rects (on top), so check circles first.
        var circles = frame.ShapesSave!.CircleSaves;
        for (int i = circles.Count - 1; i >= 0; i--)
        {
            var c = circles[i];
            float sx = cx + c.X * om;
            float sy = cy - c.Y * om;
            if (PreviewShapeHitTester.HitsCircle(px, py, sx, sy, c.Radius * om, tolerance))
            {
                _selectedState!.SelectedCircle = c;
                return true;
            }
        }

        var rects = frame.ShapesSave!.AARectSaves;
        for (int i = rects.Count - 1; i >= 0; i--)
        {
            var r = rects[i];
            float sx = cx + r.X * om;
            float sy = cy - r.Y * om;
            if (PreviewShapeHitTester.HitsRect(px, py, sx, sy, r.ScaleX * om, r.ScaleY * om, tolerance))
            {
                _selectedState!.SelectedRectangle = r;
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

        // Click in left ruler strip ΓåÆ create horizontal guide
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

        // Click in top ruler strip ΓåÆ create vertical guide
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

        // Click near existing guide ΓåÆ drag it
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

        // No guide hit — try to drag a shape (or just select one).

        // Check resize handles on the selected shape first.
        var handleKind = HitTestShapeHandle(px, py);
        if (handleKind != HandleKind.None)
        {
            var sel = _selectedState!.SelectedShape!;
            _draggingShape   = sel;
            _shapeDragAnchor = pos;
            if (sel is AARectSave dhr)
            {
                _shapeDragStartX      = dhr.X;
                _shapeDragStartY      = dhr.Y;
                _shapeDragStartScaleX = dhr.ScaleX;
                _shapeDragStartScaleY = dhr.ScaleY;
            }
            else if (sel is CircleSave dhc)
            {
                _shapeDragStartX      = dhc.X;
                _shapeDragStartY      = dhc.Y;
                _shapeDragStartScaleX = dhc.Radius;
                _shapeDragStartScaleY = 0f;
            }
            _shapeResizeHandle = handleKind;
            e.Pointer.Capture(this);
            return;
        }
        var hitShape = HitTestShape(px, py);
        if (hitShape is not null)
        {
            _selectedState!.SelectedNodes = new System.Collections.Generic.List<object>();
            if (hitShape is AARectSave hr) _selectedState!.SelectedRectangle = hr;
            else if (hitShape is CircleSave hc)          _selectedState!.SelectedCircle    = hc;
            _draggingShape   = hitShape;
            _shapeDragAnchor = pos;
            if (hitShape is AARectSave dsr) { _shapeDragStartX = dsr.X; _shapeDragStartY = dsr.Y; }
            else if (hitShape is CircleSave dsc)          { _shapeDragStartX = dsc.X; _shapeDragStartY = dsc.Y; }
            e.Pointer.Capture(this);
            return;
        }

        // No shape hit — try to select a collision shape.
        TrySelectShapeAt(px, py);
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


        if (_shapeResizeHandle != HandleKind.None)
        {
            ApplyShapeResize(pos);
            InvalidateVisual();
            return;
        }

        if (_draggingShape is not null)
        {
            float om = _appState!.OffsetMultiplier * _zoom;
            float dx = (float)(pos.X - _shapeDragAnchor.X) / om;
            float dy = -(float)(pos.Y - _shapeDragAnchor.Y) / om;
            float newX = _shapeDragStartX + dx;
            float newY = _shapeDragStartY + dy;
            if (_draggingShape is AARectSave r) { r.X = newX; r.Y = newY; }
            else if (_draggingShape is CircleSave c)          { c.X = newX; c.Y = newY; }
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


        if (_shapeResizeHandle != HandleKind.None)
        {
            CommitShapeResize();
            e.Pointer.Capture(null);
            return;
        }


        if (_draggingShape is not null)
        {
            CommitShapeDrag();
            e.Pointer.Capture(null);
            return;
        }

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

    // -- Inner types -----------------------------------------------------------

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

    // -- Shared SkiaSharp rendering (used by both live and off-screen paths) --

    private static void RenderSkCore(
        SKCanvas canvas, RenderSnapshot s, Dictionary<string, SKBitmap?> cache)
    {
        canvas.Clear(new SKColor(30, 30, 30));

        // Content origin is shifted so the ruler strips sit at the left/top edges
        float cx = (s.Width  - RulerSize) / 2f + RulerSize + s.PanX;
        float cy = (s.Height - RulerSize) / 2f + RulerSize + s.PanY;

        // Clip content/guide drawing to the non-ruler area.
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

        // Collision shapes (AxisAlignedRectangles and Circles).
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
                    if (sh.IsSelected)
                        DrawShapeHandles(canvas, sx - hw, sy - hh, sx + hw, sy + hh);
                    canvas.DrawRect(new SKRect(sx - hw, sy - hh, sx + hw, sy + hh), paint);
                }
                else
                {
                    float sr = sh.Param1 * om;
                    if (sh.IsSelected)
                    {
                        // Draw the bounding square outline for the circle so handles have context.
                        using var boxPaint = new SKPaint
                        {
                            Color       = new SKColor(255, 220, 0, 120),
                            Style       = SKPaintStyle.Stroke,
                            StrokeWidth = 1f,
                            PathEffect  = SKPathEffect.CreateDash(new float[] { 4f, 4f }, 0f),
                        };
                        DrawShapeHandles(canvas, sx - sr, sy - sr, sx + sr, sy + sr);
                        canvas.DrawRect(new SKRect(sx - sr, sy - sr, sx + sr, sy + sr), boxPaint);
                    }
                    canvas.DrawCircle(sx, sy, sh.Param1 * om, paint);
                }
            }
        }

        // Dragged guide value label - drawn last so it stays on top of shapes.
        if (s.DraggedGuideIdx >= 0)
        {
            using var dragLabelFont = new SKFont { Size = 11f };
            using var dragLabelPaint = new SKPaint
            {
                Color       = new SKColor(0, 200, 255, 230),
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
                    canvas.DrawText(FormatGuideLabel(true, wy), RulerSize + lMargin, baseline, dragLabelFont, dragLabelPaint);
                }
            }
            else if (!s.DraggingHGuide && s.DraggedGuideIdx < s.VGuides.Length)
            {
                float wx = s.VGuides[s.DraggedGuideIdx];
                float sx = cx + wx * s.Zoom;
                if (sx >= RulerSize && sx <= s.Width)
                {
                    float lx = Math.Clamp(sx + lMargin, RulerSize + lMargin, s.Width - 50f);
                    canvas.DrawText(FormatGuideLabel(false, wx), lx, RulerSize + lHeight, dragLabelFont, dragLabelPaint);
                }
            }
        }

        canvas.Restore(); // end content clip

        // Ruler strips.
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
        using var labelFont = new SKFont { Size = 8f };
        using var labelPaint = new SKPaint
        {
            Color    = new SKColor(190, 190, 195),
            IsAntialias = true
        };

        float majorStep = GetRulerStep(s.Zoom);
        float minorStep = majorStep / 5f;

        // Top (horizontal) ruler - ticks at world-X positions.
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
                canvas.DrawText(((int)MathF.Round(wx)).ToString(), sx + 1f, RulerSize - tickH - 1f, labelFont, labelPaint);
        }

        // Left (vertical) ruler - ticks at world-Y positions.
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
                canvas.DrawText(((int)MathF.Round(wy)).ToString(), 0f, 0f, labelFont, labelPaint);
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
            Color = new SKColor(255, 255, 255, (byte)(255 * alpha))
        };
        var sampling = zoom >= 1
            ? new SKSamplingOptions(SKFilterMode.Nearest)
            : new SKSamplingOptions(SKFilterMode.Linear);

        bool flip = FlipScaleCalculator.IsFlipped(frame.FlipHorizontal, frame.FlipVertical);
        if (flip)
        {
            canvas.Save();
            var (scaleX, scaleY) = FlipScaleCalculator.Compute(frame.FlipHorizontal, frame.FlipVertical);
            canvas.Scale(scaleX, scaleY, cx, cy);
        }

        using var img = SKImage.FromBitmap(bm);
        canvas.DrawImage(img, src, dst, sampling, paint);

        if (flip) canvas.Restore();

        using var op = new SKPaint
        {
            Color       = new SKColor(255, 255, 255, (byte)(200 * alpha)),
            StrokeWidth = 1f,
            IsStroke    = true
        };
        canvas.DrawRect(dst, op);
    }

    /// <summary>
    /// Draws the 8 resize handles (white fill + DodgerBlue stroke) outside the bounding box
    /// defined by <paramref name="left"/>, <paramref name="top"/>, <paramref name="right"/>,
    /// <paramref name="bottom"/>. All coordinates are in screen space.
    /// </summary>
    private static void DrawShapeHandles(SKCanvas canvas, float left, float top, float right, float bottom)
    {
        const float Hs = 5f; // half the handle square side length
        float cx = (left + right)   / 2f;
        float cy = (top  + bottom)  / 2f;

        (float x, float y)[] pts =
        {
            (left  - Hs, top    - Hs), (cx, top    - Hs), (right + Hs, top    - Hs),
            (left  - Hs, cy),                              (right + Hs, cy),
            (left  - Hs, bottom + Hs), (cx, bottom + Hs), (right + Hs, bottom + Hs),
        };

        using var fill   = new SKPaint { Color = SKColors.White,     Style = SKPaintStyle.Fill };
        using var stroke = new SKPaint { Color = SKColors.DodgerBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };

        foreach (var (hx, hy) in pts)
        {
            var hr = new SKRect(hx - Hs, hy - Hs, hx + Hs, hy + Hs);
            canvas.DrawRect(hr, fill);
            canvas.DrawRect(hr, stroke);
        }
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
