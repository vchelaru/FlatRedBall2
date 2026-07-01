using AnimationEditor.App.Services;
using AnimationEditor.App.Theming;
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
using Avalonia.Styling;
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

    // -- Camera ----------------------------------------------------------------
    private float _zoom = 1f;
    private float _panX, _panY;

    // -- Smooth (animated) wheel zoom (#451) -----------------------------------
    // Mirrors WireframeControl's #425 smooth zoom: a wheel notch retargets _zoomTarget and the
    // timer eases _zoom toward it via ZoomChase, applying each stepped value through the
    // pivot-preserving ZoomToward. The pivot is held in viewport space and re-used every tick, so
    // the point under the cursor stays fixed for the whole animation. Rapid notches retarget the
    // in-flight animation rather than stacking.
    private DispatcherTimer? _zoomTimer;
    private bool  _zoomAnimating;
    private float _zoomTarget;      // destination zoom factor (1.0 = 100 %)
    private float _zoomPivotVpX;    // cursor pivot, viewport space
    private float _zoomPivotVpY;
    private const float ZoomAnimIntervalSeconds = 1f / 60f;

    // -- Render diagnostics (#514) ---------------------------------------------
    // When enabled, overlays the rolling-average Skia render time (ms/frame + fps) top-left.
    // Measures the compositor-thread render — where the cost actually lands (the UI-thread
    // Render() only builds the snapshot). Toggled at runtime via DiagnosticsEnabled; MainWindow
    // wires F3 and the Help menu item to flip this in lock-step with the wireframe panel.
    private bool _showDiagnostics;
    private readonly RollingAverage _drawTimes = new(10);
    private DispatcherTimer? _diagnosticsTimer;

    /// <summary>
    /// Shows/hides the draw-time diagnostics overlay. Toggled at runtime from MainWindow
    /// (F3 and the Help ▸ Show Render Diagnostics menu item).
    /// </summary>
    public bool DiagnosticsEnabled
    {
        get => _showDiagnostics;
        set
        {
            if (_showDiagnostics == value) return;
            _showDiagnostics = value;
            // The panel only repaints on demand (playback/zoom/pan), so an idle overlay would show
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

    // -- Settings --------------------------------------------------------------
    private bool _showOnionSkin;
    private bool _showGuides;
    private bool _interpolateOffsets;

    // -- Pan drag --------------------------------------------------------------
    private bool  _isPanning;
    private Point _lastMousePt;

    // -- Rulers / guides -------------------------------------------------------
    private const float RulerSize = 20f;
    private const float PanPadding = 0f;

    // Neutral canvas/ruler colors for the active theme variant. Refreshed from
    // ActualThemeVariant on every render and whenever the variant changes.
    private CanvasPalette _palette = CanvasPalette.Dark;
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

    /// <summary>
    /// When <c>true</c>, the displayed sprite position eases between consecutive frames'
    /// <c>RelativeX/Y</c> offsets instead of snapping at each frame switch. Preview-only:
    /// the FlatRedBall runtime always snaps, so this does not reflect in-game behavior and
    /// is not persisted. Auto-resets to <c>false</c> whenever the selected chain changes.
    /// </summary>
    public bool InterpolateOffsets
    {
        get => _interpolateOffsets;
        set
        {
            if (_interpolateOffsets == value) return;
            _interpolateOffsets = value;
            InvalidateVisual();
            InterpolateOffsetsChanged?.Invoke(value);
        }
    }

    /// <summary>Fired when <see cref="InterpolateOffsets"/> changes, including the automatic
    /// reset on selection change, so the toolbar toggle can resync its checked state.</summary>
    public event Action<bool>? InterpolateOffsetsChanged;

    public double SpeedMultiplier
    {
        get => _playback.SpeedMultiplier;
        set => _playback.SpeedMultiplier = value;
    }

    public void Play()  => _playback.Play();
    public void Pause() => _playback.Pause();
    public void StopPlayback() { _playback.Reset(); InvalidateVisual(); }

    /// <summary>Whether the animation is currently playing.</summary>
    public bool IsPlaying => _playback.IsPlaying;

    /// <summary>Fires when play/pause state changes, so a transport button can resync its icon.</summary>
    public event Action<bool>? IsPlayingChanged
    {
        add    => _playback.IsPlayingChanged += value;
        remove => _playback.IsPlayingChanged -= value;
    }

    /// <summary>Toggles between playing (<see cref="ResumePlayback"/>) and paused (<see cref="PausePlayback"/>).</summary>
    public void TogglePlayPause()
    {
        if (_playback.IsPlaying) PausePlayback();
        else                     ResumePlayback();
    }

    /// <summary>
    /// Resumes playback from the current playhead position, clearing any pinned-frame selection so
    /// the preview animates again. Does not reset to the start (#432: resume-from-playhead).
    /// </summary>
    public void ResumePlayback()
    {
        // Clearing the pinned frame lets OnSelectionChanged keep the playback position (it only
        // re-seeks when the chain changes), so playback continues from where the playhead sits.
        if (_selectedState!.SelectedFrame is not null)
            _selectedState!.SelectedFrame = null;
        _playback.Play();
        _timer.Start();
        InvalidateVisual();
    }

    /// <summary>
    /// Pauses playback and pins the frame currently showing so the inspector/wireframe reflect it.
    /// Keeps the sub-frame playhead position (does not snap to the frame's start).
    /// </summary>
    public void PausePlayback()
    {
        _playback.Pause();
        var chain = _selectedState!.SelectedChain;
        if (chain is not null && chain.Frames.Count > 0)
        {
            int idx = Math.Clamp(_playback.CurrentFrameIndex, 0, chain.Frames.Count - 1);
            var frame = chain.Frames[idx];
            if (!ReferenceEquals(_selectedState!.SelectedFrame, frame))
                _selectedState!.SelectedFrame = frame;
        }
        InvalidateVisual();
    }

    /// <summary>
    /// Scrubs to <paramref name="frameIndex"/> at <paramref name="fraction"/> through that frame:
    /// pauses, seeks playback, and selects that frame so the inspector/wireframe follow. The
    /// selection does not snap the playhead back to the frame start because the seek already set
    /// the sub-frame position (#432).
    /// </summary>
    public void ScrubToFrame(int frameIndex, double fraction)
    {
        _playback.Pause();
        _playback.SeekToFrame(frameIndex, fraction);
        var chain = _selectedState!.SelectedChain;
        if (chain is not null && frameIndex >= 0 && frameIndex < chain.Frames.Count)
        {
            var frame = chain.Frames[frameIndex];
            if (!ReferenceEquals(_selectedState!.SelectedFrame, frame))
                _selectedState!.SelectedFrame = frame;
        }
        InvalidateVisual();
    }

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

        string? texPath   = _thumbnailService!.ResolveTexturePath(displayFrame);
        string? onionPath = _thumbnailService!.ResolveTexturePath(onionFrame);
        // GetImage warms both the image cache (drawn below) and the bitmap cache it decodes from.
        _thumbnailService.GetImage(texPath);
        _thumbnailService.GetImage(onionPath);

        var (frameOffX, frameOffY) = ResolveFrameOffset(chain, displayFrame, selectedFrame is not null);

        var snap   = new RenderSnapshot(displayFrame, onionFrame, _zoom, _panX, _panY,
                                        _showGuides, texPath, onionPath, width, height,
                                        _appState!.OffsetMultiplier,
                                        _hGuides.ToArray(), _vGuides.ToArray(),
                                        _draggedGuideIdx, _draggingHGuide,
                                        BuildShapeInfos(), frameOffX, frameOffY);
        UpdatePalette();
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        RenderSkCore(canvas, snap, _thumbnailService.ImageCache, _palette);
        return bitmap;
    }

    // -- Injected services -----------------------------------------------------

    private ISelectedState? _selectedState;
    private IAppState? _appState;
    private IAppCommands? _appCommands;
    private IApplicationEvents? _events;
    private IProjectManager? _projectManager;
    private IUndoManager? _undoManager;
    private ThumbnailService? _thumbnailService;
    private IPendingCutState? _pendingCutState;

    private static readonly SKColor CutOutlineColor = new(224, 112, 48, 220);

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
        IUndoManager undoManager,
        ThumbnailService thumbnailService,
        IPendingCutState pendingCutState)
    {
        _selectedState  = selectedState;
        _appState       = appState;
        _appCommands    = appCommands;
        _events         = events;
        _projectManager = projectManager;
        _undoManager    = undoManager;
        _thumbnailService = thumbnailService;
        _pendingCutState  = pendingCutState;

        _selectedState.SelectionChanged                        += () => Dispatcher.UIThread.InvokeAsync(OnSelectionChanged);
        _pendingCutState.Changed                               += () => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
        // Content edits change the union extent, hence the scrollbar range — refresh alongside the repaint.
        _events.AnimationChainsChanged                         += () => Dispatcher.UIThread.InvokeAsync(() => { InvalidateVisual(); RaiseViewChanged(); });
        _events.AchxLoaded                                     += _ => Dispatcher.UIThread.InvokeAsync(OnSelectionChanged);
        _appCommands.RefreshAnimationFrameDisplayRequested     += () => Dispatcher.UIThread.InvokeAsync(() => { InvalidateVisual(); RaiseViewChanged(); });
    }

    // -- Constructor -----------------------------------------------------------

    public PreviewControl()
    {
        ClipToBounds = true;
        Focusable    = true;

        // Repaint when the app theme variant changes so the canvas/ruler colors update.
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();

        // A resize changes the viewport extent, hence the scrollbar range — refresh the host's scrollbars.
        SizeChanged += (_, _) => RaiseViewChanged();

        // Stop the smooth-zoom and diagnostics timers if the control leaves the tree.
        DetachedFromVisualTree += (_, _) => { StopZoomTimer(); _diagnosticsTimer?.Stop(); };

        // Subscriptions are deferred to InitializeServices (called from MainWindow)

        _playback.FrameIndexChanged += _ => Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
        // While interpolating, repaint every tick so the eased motion is smooth, not stepped
        // to once-per-frame-switch like the snap path.
        _playback.PlaybackTicked += () =>
        {
            if (_interpolateOffsets) Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
        };

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
        var chain = _selectedState!.SelectedChain;
        var frame = _selectedState!.SelectedFrame;

        // Only reload (which resets to the start) when the chain itself changes; a frame select or
        // a resume on the same chain must keep the current playhead position.
        if (!ReferenceEquals(chain, _playback.Chain))
            _playback.SetChain(chain);

        if (frame is not null && chain is not null)
        {
            // Selecting a frame pauses at its start — but skip the seek when playback is already on
            // that frame, so a scrub/pause that set a sub-frame position does not snap to the start.
            int idx = chain.Frames.IndexOf(frame);
            if (idx >= 0 && _playback.CurrentFrameIndex != idx)
                _playback.SeekToFrame(idx);
            _playback.Pause();
        }
        else if (chain is not null)
        {
            // Selecting a whole animation auto-plays, overriding any prior paused state.
            _playback.Play();
        }

        // Interpolation is a transient per-chain preview aid; clear it when the selection changes.
        InterpolateOffsets = false;
        InvalidateVisual();
        // A new chain/frame changes the union extent → refresh the host's scrollbars.
        RaiseViewChanged();
    }

    /// <summary>
    /// Resolves the (X, Y) offset for the display frame. During free playback the offset
    /// honors <see cref="InterpolateOffsets"/>; when a frame is pinned via tree selection it
    /// is shown statically at its own stored offset.
    /// </summary>
    private (float X, float Y) ResolveFrameOffset(
        AnimationChainSave? chain, AnimationFrameSave? displayFrame, bool pinned)
    {
        if (pinned || displayFrame is null)
            return (displayFrame?.RelativeX ?? 0f, displayFrame?.RelativeY ?? 0f);

        return OffsetInterpolator.ComputeOffset(
            chain, _playback.CurrentFrameIndex, _playback.FrameElapsed, _interpolateOffsets);
    }

    // -- Public API ------------------------------------------------------------

    /// <summary>
    /// Fired after every zoom change. Payload is the new zoom as a percentage
    /// (e.g. 100f = 100 %).
    /// </summary>
    public event Action<float>? ZoomChanged;

    /// <summary>Fired when the user finishes a pan gesture (pointer released after drag).</summary>
    public event Action<float, float>? PanChanged;

    /// <summary>
    /// Fired whenever the view (pan, zoom, content extent, or viewport size) changes, so the
    /// host can refresh the Preview's scrollbars (#415). Distinct from <see cref="PanChanged"/>,
    /// which only fires on pan-gesture completion for persistence.
    /// </summary>
    public event Action? ViewChanged;

    private void RaiseViewChanged() => ViewChanged?.Invoke();

    /// <summary>
    /// When non-null, mouse-wheel zoom steps through these preset percentages instead
    /// of applying a raw ×1.25/×0.8 multiplier.  Set by <c>MainWindow</c> on startup.
    /// <para>
    /// Standalone controls (no <c>MainWindow</c>) leave this null and retain the legacy
    /// multiplier behaviour, which is still useful in precision-zoom tests.
    /// </para>
    /// </summary>
    public int[]? WheelZoomPresets { get; set; }

    public void SetZoomPercent(int pct)
    {
        CancelZoomAnimation();   // an explicit zoom overrides any in-flight wheel ease
        _zoom = Math.Clamp(pct / 100f, CanvasTransform.MinZoom, CanvasTransform.MaxZoom);
        ClampPan();
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoom * 100f);
        RaiseViewChanged();
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
        CancelZoomAnimation();   // an explicit recentre overrides any in-flight wheel ease
        float offMult = _appState!.OffsetMultiplier;
        _panX = -entityX * offMult * _zoom;
        _panY =  entityY * offMult * _zoom;
        InvalidateVisual();
        RaiseViewChanged();
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
    /// control-space point and runs the resulting smooth-zoom animation to completion
    /// synchronously, so the camera lands on its settled state. Mirrors
    /// <see cref="OnPointerWheelChanged"/>. Use <see cref="SimulateWheelZoomBegin"/> instead to
    /// observe the animation mid-flight.
    /// </summary>
    public void SimulateWheelZoom(double x, double y, bool zoomIn)
    {
        BeginAnimatedZoom((float)x, (float)y, zoomIn);
        SettleZoomAnimation();
    }

    // -- Smooth (animated) wheel zoom (#451) -----------------------------------

    /// <summary>
    /// Test-only: begins a smooth wheel-zoom toward the <b>control-space</b> pivot WITHOUT settling,
    /// so a test can drive <see cref="StepZoomAnimation"/> tick-by-tick and observe the ease and
    /// retargeting. Mirrors the live <see cref="OnPointerWheelChanged"/> path.
    /// </summary>
    public void SimulateWheelZoomBegin(float vpX, float vpY, bool zoomIn) =>
        BeginAnimatedZoom(vpX, vpY, zoomIn);

    /// <summary>True while a smooth wheel-zoom (#451) is easing toward its target. The host gates
    /// companion-file persistence on this so only the settled state is saved, not every tick.</summary>
    public bool IsZoomAnimating => _zoomAnimating;

    /// <summary>Test-only: the zoom factor the in-flight animation is easing toward (1.0 = 100 %).</summary>
    public float TargetZoom => _zoomTarget;

    /// <summary>
    /// Advances the in-flight smooth zoom by <paramref name="dtSeconds"/>, easing toward the target
    /// via <see cref="ZoomChase"/> and applying each step through the pivot-preserving
    /// <see cref="ApplyZoomTowardPivot"/>. Returns <c>true</c> while still animating, <c>false</c>
    /// once settled (at which point the timer is stopped). The live 60 fps timer calls this; tests
    /// call it directly for deterministic stepping.
    /// </summary>
    public bool StepZoomAnimation(float dtSeconds)
    {
        if (!_zoomAnimating) return false;

        float next = ZoomChase.Step(_zoom, _zoomTarget, dtSeconds);
        bool settling = ZoomChase.IsSettled(next, _zoomTarget);

        // Clear the flag BEFORE the apply fires ZoomChanged on the settling tick, so the host sees
        // IsZoomAnimating == false and persists the companion file exactly once (on settle).
        if (settling) { _zoomAnimating = false; StopZoomTimer(); }

        // factor is relative to the current zoom; the viewport pivot is constant across ticks, so
        // the factors compose to the same result as a single notch (the pivot stays anchored).
        ApplyZoomTowardPivot(_zoomPivotVpX, _zoomPivotVpY, next / _zoom);

        // On the settling tick, snap the zoom scalar exactly onto the target. The per-tick factor
        // multiplications accumulate float drift (e.g. 1.5000001 instead of 1.5), which would make
        // a preset-stepping zoom button mis-read the current preset and fail to step (#451).
        if (settling) _zoom = _zoomTarget;
        return !settling;
    }

    /// <summary>Runs <see cref="StepZoomAnimation"/> to completion synchronously. Used by the
    /// instant <see cref="SimulateWheelZoom"/> overload and any caller that must force settle.</summary>
    public void SettleZoomAnimation()
    {
        // The 1000-iteration cap is a non-convergence backstop; ZoomChase settles far sooner.
        for (int i = 0; _zoomAnimating && i < 1000; i++)
            StepZoomAnimation(ZoomAnimIntervalSeconds);
    }

    /// <summary>
    /// Retargets the smooth zoom toward the next/previous preset from the given control-space pivot.
    /// A notch while already animating steps from the in-flight <see cref="_zoomTarget"/>, so rapid
    /// spins accumulate through the presets rather than re-targeting the same one from the
    /// mid-animation zoom. Does NOT start the driving timer — the live wheel handler starts it; tests
    /// drive <see cref="StepZoomAnimation"/> directly for determinism.
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
    /// Competing camera actions (pan, combo zoom, centre-on-point) call this so they don't fight the
    /// easing timer.</summary>
    private void CancelZoomAnimation()
    {
        if (!_zoomAnimating) return;
        _zoomAnimating = false;
        StopZoomTimer();
    }

    /// <summary>The zoom factor one wheel notch targets from <paramref name="basisZoom"/>, using
    /// preset stepping when <see cref="WheelZoomPresets"/> is set, else a ×1.25/×0.8 multiplier.
    /// Clamped to [<see cref="CanvasTransform.MinZoom"/>, <see cref="CanvasTransform.MaxZoom"/>].</summary>
    private float ComputeTargetZoom(float basisZoom, bool zoomIn)
    {
        float targetPct = WheelZoomPresets is { Length: > 0 } presets
            ? ZoomPresetStepper.StepToNextPreset(basisZoom * 100f, presets, zoomIn ? +1 : -1)
            : basisZoom * 100f * (zoomIn ? 1.25f : 0.8f);
        return Math.Clamp(targetPct / 100f, CanvasTransform.MinZoom, CanvasTransform.MaxZoom);
    }

    /// <summary>
    /// Applies a zoom <paramref name="factor"/> relative to the current zoom, holding the
    /// control-space pivot (<paramref name="vpX"/>, <paramref name="vpY"/>) fixed. Updates pan/zoom,
    /// clamps, repaints, and fires <see cref="ZoomChanged"/> / <see cref="ViewChanged"/>.
    /// </summary>
    private void ApplyZoomTowardPivot(float vpX, float vpY, float factor)
    {
        float oldZoom = _zoom;

        // Preview pan is relative to the canvas center; convert to the absolute pan
        // (the origin's screen position) that CanvasTransform.ZoomToward operates on,
        // then convert the result back.
        float cx0 = (float)((Bounds.Width  - RulerSize) / 2f + RulerSize);
        float cy0 = (float)((Bounds.Height - RulerSize) / 2f + RulerSize);
        var (absPanX, absPanY, newZoom) = CanvasTransform.ZoomToward(
            vpX, vpY, factor, cx0 + _panX, cy0 + _panY, oldZoom);

        _zoom = newZoom;
        _panX = absPanX - cx0;
        _panY = absPanY - cy0;
        ClampPan();
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoom * 100f);
        RaiseViewChanged();
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

    public void SetPan(float panX, float panY)
    {
        _panX = panX;
        _panY = panY;
        InvalidateVisual();
        RaiseViewChanged();
    }

    /// <summary>
    /// Sets the horizontal pan from a scrollbar-driven value (see <see cref="PanScrollBar"/>)
    /// and repaints. Clamped defensively to the pan band.
    /// </summary>
    public void SetPanX(float panX)
    {
        _panX = panX;
        ClampPan();
        InvalidateVisual();
        RaiseViewChanged();
    }

    /// <summary>
    /// Sets the vertical pan from a scrollbar-driven value (see <see cref="PanScrollBar"/>)
    /// and repaints. Clamped defensively to the pan band.
    /// </summary>
    public void SetPanY(float panY)
    {
        _panY = panY;
        ClampPan();
        InvalidateVisual();
        RaiseViewChanged();
    }

    /// <summary>
    /// Keeps the displayed content within reach so it never drifts fully off-screen, while
    /// letting the user scroll across it. The pan band scales with the content's on-screen
    /// extent (content × zoom), so zooming in on content offset from the entity origin never
    /// pins it to one edge (#412). No-op until layout has run (<c>Bounds.Width > 1</c>).
    /// </summary>
    private void ClampPan()
    {
        if (Bounds.Width <= 1) return;

        float viewW = (float)(Bounds.Width  - RulerSize);
        float viewH = (float)(Bounds.Height - RulerSize);

        var (minX, maxX, minY, maxY) = ComputeContentExtentScreenPx();
        (_panX, _panY) = CanvasTransform.ClampPan(
            _panX, _panY, viewW, viewH, minX, maxX, minY, maxY, PanPadding);
    }

    /// <summary>
    /// Bounding box of the chain's content (every frame's sprite footprint + collision shapes)
    /// in screen pixels, relative to the entity origin (world Y up is flipped to screen Y down).
    /// The origin itself is always included, so empty content collapses the band back to the
    /// simple "origin stays on screen" clamp.
    /// <para>
    /// The union of <b>all</b> frames (at their resting <c>RelativeX/Y</c> offsets) is used, not
    /// the current playback frame, so the extent — and the scrollbar range and pan clamp derived
    /// from it — stays stable during playback instead of breathing frame-to-frame (#415).
    /// </para>
    /// </summary>
    private (float MinX, float MaxX, float MinY, float MaxY) ComputeContentExtentScreenPx()
    {
        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
        float om = _appState!.OffsetMultiplier * _zoom;

        void Include(float cx, float cy, float halfW, float halfH)
        {
            minX = Math.Min(minX, cx - halfW); maxX = Math.Max(maxX, cx + halfW);
            minY = Math.Min(minY, cy - halfH); maxY = Math.Max(maxY, cy + halfH);
        }

        void IncludeFrame(AnimationFrameSave frame)
        {
            // Sprite footprint at the frame's resting offset. Pixel size scales by zoom only
            // (not OffsetMultiplier). Skip it when the texture isn't cached yet — the offset
            // center alone still keeps the frame reachable.
            string? texPath = _thumbnailService!.ResolveTexturePath(frame);
            if (texPath is not null &&
                _thumbnailService.BitmapCache.TryGetValue(texPath, out var bm) && bm is not null)
            {
                var (_, _, sw, sh) = ComputeSourceRect(frame, bm.Width, bm.Height);
                Include(frame.RelativeX * om, -frame.RelativeY * om, sw * _zoom / 2f, sh * _zoom / 2f);
            }

            if (frame.ShapesSave is null) return;
            foreach (var r in frame.ShapesSave.AARectSaves)
                Include(r.X * om, -r.Y * om, r.ScaleX * om, r.ScaleY * om);
            foreach (var c in frame.ShapesSave.CircleSaves)
                Include(c.X * om, -c.Y * om, c.Radius * om, c.Radius * om);
        }

        var chain = _selectedState!.SelectedChain;
        if (chain is not null)
            foreach (var frame in chain.Frames) IncludeFrame(frame);
        else if (_selectedState!.SelectedFrame is not null)
            IncludeFrame(_selectedState!.SelectedFrame);

        return (minX, maxX, minY, maxY);
    }

    /// <summary>
    /// Scrollbar (Minimum, Maximum, Value, ViewportSize) for each axis, derived from the
    /// current pan, viewport, and content extent. <c>MainWindow</c> applies these to the
    /// Preview's two <c>ScrollBar</c>s; the value axis is the negation of the pan axis (see
    /// <see cref="PanScrollBar"/>). Returns a degenerate (zero) range before layout has run.
    /// </summary>
    public (ScrollBarRange Horizontal, ScrollBarRange Vertical) GetScrollBarRanges()
    {
        float viewW = (float)(Bounds.Width  - RulerSize);
        float viewH = (float)(Bounds.Height - RulerSize);
        if (viewW <= 0 || viewH <= 0)
            return (new ScrollBarRange(0f, 0f, 0f, 1f), new ScrollBarRange(0f, 0f, 0f, 1f));

        var (minX, maxX, minY, maxY) = ComputeContentExtentScreenPx();
        return (PanScrollBar.FromPan(_panX, viewW, minX, maxX, PanPadding),
                PanScrollBar.FromPan(_panY, viewH, minY, maxY, PanPadding));
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

        // Pre-fill the image cache synchronously before handing off to the render thread.
        // GetImage warms both the image cache (drawn by the render op) and the bitmap cache it
        // decodes from — the cached SKImage is what avoids a per-frame full-atlas copy (#514).
        string? texPath   = _thumbnailService!.ResolveTexturePath(displayFrame);
        string? onionPath = _thumbnailService!.ResolveTexturePath(onionFrame);
        _thumbnailService.GetImage(texPath);
        _thumbnailService.GetImage(onionPath);

        var (frameOffX, frameOffY) = ResolveFrameOffset(chain, displayFrame, selectedFrame is not null);

        UpdatePalette();
        ctx.Custom(new DrawOp(
            new RenderSnapshot(
                displayFrame, onionFrame, _zoom, _panX, _panY, _showGuides,
                texPath, onionPath, (float)w, (float)h,
                _appState!.OffsetMultiplier,
                _hGuides.ToArray(), _vGuides.ToArray(),
                _draggedGuideIdx, _draggingHGuide,
                BuildShapeInfos(), frameOffX, frameOffY),
            _thumbnailService.ImageCache, _palette, _showDiagnostics ? _drawTimes : null));
    }

    // ActualThemeVariant resolves Default to the concrete platform variant, so a simple
    // "is it Light?" check correctly handles the follow-system case.
    private void UpdatePalette() => _palette = CanvasPalette.For(ActualThemeVariant != ThemeVariant.Light);

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

    /// <summary>Test-only: world-space point under a control-space screen point at the current
    /// camera. Mirrors the pivot math the smooth-zoom animation preserves.</summary>
    internal (float X, float Y) ScreenToWorldForTest(float screenX, float screenY)
        => (ScreenToWorldX(screenX), ScreenToWorldY(screenY));

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
    /// Replaces all guides with the provided world-coordinate values.
    /// Used to restore guide state from the companion .aeproperties file.
    /// </summary>
    internal void SetGuides(IReadOnlyList<float> hGuides, IReadOnlyList<float> vGuides)
    {
        _hGuides.Clear();
        _hGuides.AddRange(hGuides);
        _vGuides.Clear();
        _vGuides.AddRange(vGuides);
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
    /// Captures a thread-safe snapshot of collision shapes for the current display frame.
    /// When a frame is pinned via tree selection, shapes come from that frame and selection
    /// highlighting is applied. During free playback (no pinned frame), shapes come from the
    /// currently-playing frame and are all rendered in the unselected style.
    /// </summary>
    private PreviewShapeInfo[] BuildShapeInfos()
    {
        var pinnedFrame = _selectedState!.SelectedFrame;
        var frame = pinnedFrame ?? GetCurrentPlaybackFrame();
        if (frame?.ShapesSave is null) return Array.Empty<PreviewShapeInfo>();

        var selectedRects   = new HashSet<AARectSave>();
        var selectedCircles = new HashSet<CircleSave>();

        if (pinnedFrame is not null)
        {
            selectedRects = _selectedState!.SelectedRectangles.ToHashSet();
            if (_selectedState!.SelectedRectangle is { } sr) selectedRects.Add(sr);
            selectedCircles = _selectedState!.SelectedCircles.ToHashSet();
            if (_selectedState!.SelectedCircle is { } sc) selectedCircles.Add(sc);
        }

        var list = new List<PreviewShapeInfo>();
        var pendingShapes = _pendingCutState?.WireframeShapes.ToHashSet() ?? [];
        foreach (var r in frame.ShapesSave!.AARectSaves)
            list.Add(new PreviewShapeInfo(PreviewShapeKind.Rect, r.X, r.Y, r.ScaleX, r.ScaleY,
                selectedRects.Contains(r), pendingShapes.Contains(r)));
        foreach (var c in frame.ShapesSave!.CircleSaves)
            list.Add(new PreviewShapeInfo(PreviewShapeKind.Circle, c.X, c.Y, c.Radius, 0f,
                selectedCircles.Contains(c), pendingShapes.Contains(c)));
        return list.ToArray();
    }

    /// <summary>Returns the frame in <see cref="ISelectedState.SelectedChain"/> at the current playback index.</summary>
    private AnimationFrameSave? GetCurrentPlaybackFrame()
    {
        var chain = _selectedState!.SelectedChain;
        if (chain is null || chain.Frames.Count == 0) return null;
        int idx = Math.Clamp(_playback.CurrentFrameIndex, 0, chain.Frames.Count - 1);
        return chain.Frames[idx];
    }

    /// <summary>Test-only: returns the shape infos that would be passed to the renderer for the current state.</summary>
    internal PreviewShapeInfo[] GetShapeInfosForTest() => BuildShapeInfos();

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

        var circles = frame.ShapesSave!.CircleSaves.ToList();
        for (int i = circles.Count - 1; i >= 0; i--)
        {
            var c = circles[i];
            if (ReferenceEquals(c, sel)) continue;
            float sx = cx + c.X * om;
            float sy = cy - c.Y * om;
            if (PreviewShapeHitTester.HitsCircle(px, py, sx, sy, c.Radius * om, tolerance))
                return c;
        }

        var rects = frame.ShapesSave!.AARectSaves.ToList();
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
        var circles = frame.ShapesSave!.CircleSaves.ToList();
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

        var rects = frame.ShapesSave!.AARectSaves.ToList();
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
        // The control IS the viewport, so e.GetPosition(this) is the control-space pivot.
        // Smooth-zoom retargets and eases toward the next preset (#451), mirroring the Wireframe.
        var pt = e.GetPosition(this);
        BeginAnimatedZoom((float)pt.X, (float)pt.Y, e.Delta.Y > 0);
        StartZoomTimer();   // live driver; tests drive StepZoomAnimation directly instead
        e.Handled = true;
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
            CancelZoomAnimation();   // a pan gesture overrides any in-flight wheel ease
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
            ClampPan();
            _lastMousePt = pos;
            InvalidateVisual();
            RaiseViewChanged();
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
        PanChanged?.Invoke(_panX, _panY);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Cursor = Cursor.Default;
    }

    // -- Inner types -----------------------------------------------------------

    internal enum PreviewShapeKind { Rect, Circle }

    /// <summary>
    /// Immutable snapshot of a single collision shape, safe to pass to the render thread.
    /// <para>For <see cref="PreviewShapeKind.Rect"/>: Param1=ScaleX, Param2=ScaleY.</para>
    /// <para>For <see cref="PreviewShapeKind.Circle"/>: Param1=Radius, Param2=0.</para>
    /// </summary>
    internal record PreviewShapeInfo(
        PreviewShapeKind Kind,
        float X, float Y,
        float Param1, float Param2,
        bool IsSelected,
        bool IsPendingCut = false);

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
        PreviewShapeInfo[] Shapes,
        // Display-frame offset, already resolved for snap vs interpolate (see ResolveFrameOffset).
        float  FrameOffsetX, float FrameOffsetY);

    // -- Shared SkiaSharp rendering (used by both live and off-screen paths) --

    private static void RenderSkCore(
        SKCanvas canvas, RenderSnapshot s, Dictionary<string, SKImage?> cache, CanvasPalette palette)
    {
        canvas.Clear(palette.Background);

        // Content origin is shifted so the ruler strips sit at the left/top edges
        float cx = (s.Width  - RulerSize) / 2f + RulerSize + s.PanX;
        float cy = (s.Height - RulerSize) / 2f + RulerSize + s.PanY;

        // Clip content/guide drawing to the non-ruler area.
        canvas.Save();
        canvas.ClipRect(new SKRect(RulerSize, RulerSize, s.Width, s.Height));

        if (s.OnionFrame is not null &&
            s.OnionTexturePath is not null &&
            cache.TryGetValue(s.OnionTexturePath, out var onionImg) && onionImg is not null)
        {
            float ocx = cx + s.OnionFrame.RelativeX * s.OffsetMultiplier * s.Zoom;
            float ocy = cy - s.OnionFrame.RelativeY * s.OffsetMultiplier * s.Zoom;
            DrawFrameCore(canvas, s.OnionFrame, onionImg, ocx, ocy, s.Zoom, alpha: 0.4f);
        }

        if (s.Frame is not null &&
            s.TexturePath is not null &&
            cache.TryGetValue(s.TexturePath, out var img) && img is not null)
        {
            float fcx = cx + s.FrameOffsetX * s.OffsetMultiplier * s.Zoom;
            float fcy = cy - s.FrameOffsetY * s.OffsetMultiplier * s.Zoom;
            DrawFrameCore(canvas, s.Frame, img, fcx, fcy, s.Zoom, alpha: 1.0f);
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
                Color       = palette.GuideLine.WithAlpha(200),
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

                if (sh.IsPendingCut)
                {
                    using var cutPaint = new SKPaint
                    {
                        Color = CutOutlineColor,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2f,
                        IsAntialias = true,
                        PathEffect = SKPathEffect.CreateDash(new float[] { 6f, 4f }, 0f),
                    };
                    if (sh.Kind == PreviewShapeKind.Rect)
                    {
                        float hw = sh.Param1 * om;
                        float hh = sh.Param2 * om;
                        canvas.DrawRect(new SKRect(sx - hw, sy - hh, sx + hw, sy + hh), cutPaint);
                    }
                    else
                        canvas.DrawCircle(sx, sy, sh.Param1 * om, cutPaint);
                }
            }
        }

        // Dragged guide value label - drawn last so it stays on top of shapes.
        if (s.DraggedGuideIdx >= 0)
        {
            using var dragLabelFont = new SKFont { Size = 11f };
            using var dragLabelPaint = new SKPaint
            {
                Color       = palette.GuideLine.WithAlpha(230),
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
        using var rulerBg = new SKPaint { Color = palette.RulerBackground };
        canvas.DrawRect(new SKRect(0,         0, s.Width, RulerSize), rulerBg);   // top
        canvas.DrawRect(new SKRect(0, RulerSize, RulerSize, s.Height), rulerBg);  // left
        canvas.DrawRect(new SKRect(0,         0, RulerSize, RulerSize), rulerBg); // corner

        using var tickPaint = new SKPaint
        {
            Color       = palette.RulerTick,
            StrokeWidth = 1f,
            IsAntialias = false
        };
        using var labelFont = new SKFont { Size = 8f };
        using var labelPaint = new SKPaint
        {
            Color    = palette.RulerLabel,
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
            bool isMajor = IsMajorTick(wx, majorStep, minorStep);
            float tickH = isMajor ? RulerSize * 0.55f : RulerSize * 0.30f;
            canvas.DrawLine(sx, RulerSize - tickH, sx, RulerSize, tickPaint);
            if (isMajor)
                canvas.DrawText(FormatRulerLabel(majorStep, wx), sx + 1f, RulerSize - tickH - 1f, labelFont, labelPaint);
        }

        // Left (vertical) ruler - ticks at world-Y positions.
        float wyStart = (RulerSize - cy) / s.Zoom;
        float wyEnd   = (s.Height  - cy) / s.Zoom;
        for (float wy = MathF.Floor(wyStart / minorStep) * minorStep; wy <= wyEnd; wy += minorStep)
        {
            float sy = cy + wy * s.Zoom;
            if (sy < RulerSize || sy > s.Height) continue;
            bool isMajor = IsMajorTick(wy, majorStep, minorStep);
            float tickW = isMajor ? RulerSize * 0.55f : RulerSize * 0.30f;
            canvas.DrawLine(RulerSize - tickW, sy, RulerSize, sy, tickPaint);
            if (isMajor)
            {
                canvas.Save();
                canvas.Translate(RulerSize - tickW - 1f, sy);
                canvas.RotateDegrees(-90f);
                canvas.DrawText(FormatRulerLabel(majorStep, wy), 0f, 0f, labelFont, labelPaint);
                canvas.Restore();
            }
        }

        // Draw guide value labels on the ruler edge
        using var guideTickPaint = new SKPaint
        {
            Color       = palette.GuideLine.WithAlpha(200),
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
        using var borderPaint = new SKPaint { Color = palette.RulerBorder, StrokeWidth = 1f };
        canvas.DrawLine(RulerSize, 0, RulerSize, s.Height, borderPaint);
        canvas.DrawLine(0, RulerSize, s.Width, RulerSize, borderPaint);
    }

    internal static bool IsMajorTick(float value, float majorStep, float minorStep)
    {
        // Use nearest-multiple distance instead of % to correctly handle negative values.
        // C# % truncates toward zero, so e.g. -3.9999998 % 2 ≈ -2 (not ≈ 0).
        float distToNearest = value - MathF.Round(value / majorStep) * majorStep;
        return MathF.Abs(distToNearest) < minorStep * 0.4f;
    }

    internal static float GetRulerStep(float zoom)
    {
        float targetWorld = 50f / zoom; // target ~50 screen px per major tick
        float[] candidates = { 0.125f, 0.25f, 0.5f, 1, 2, 5, 10, 25, 50, 100, 250, 500, 1000 };
        foreach (float c in candidates)
            if (c >= targetWorld) return c;
        return 1000f;
    }

    internal static string FormatRulerLabel(float majorStep, float worldValue) =>
        majorStep >= 1f
            ? ((int)MathF.Round(worldValue)).ToString()
            : worldValue.ToString("0.###");

    /// <summary>
    /// Converts the UV coordinates of <paramref name="frame"/> to a pixel source rect for a
    /// texture of size (<paramref name="texW"/>, <paramref name="texH"/>).
    /// Uses <see cref="FrameDisplayValues"/> (Math.Round) instead of plain <c>(int)</c>
    /// truncation so that the returned dimensions are stable across drag positions on
    /// non-power-of-2 textures (fixes preview jitter — issue #260).
    /// </summary>
    internal static (int sx, int sy, int sw, int sh) ComputeSourceRect(
        AnimationFrameSave frame, int texW, int texH)
    {
        int sx = FrameDisplayValues.GetPixelX(frame, texW);
        int sy = FrameDisplayValues.GetPixelY(frame, texH);
        int sw = FrameDisplayValues.GetPixelWidth(frame, texW);
        int sh = FrameDisplayValues.GetPixelHeight(frame, texH);
        return (sx, sy, sw, sh);
    }

    private static void DrawFrameCore(
        SKCanvas canvas, AnimationFrameSave frame, SKImage img,
        float cx, float cy, float zoom, float alpha)
    {
        var (sx, sy, sw, sh) = ComputeSourceRect(frame, img.Width, img.Height);

        var src = SKRectI.Create(sx, sy, sw, sh);
        float dw = sw * zoom;
        float dh = sh * zoom;
        float dx = cx - dw / 2;
        float dy = cy - dh / 2;
        var dst = SKRect.Create(dx, dy, dw, dh);

        using var paint = new SKPaint
        {
            // Per-frame Alpha previews as opacity (reference render); runtimes apply it however they choose.
            Color = new SKColor(255, 255, 255, FramePreviewOpacity.Resolve(frame.Alpha, alpha))
        };
        // Reference render of the frame's color operation; runtimes apply it however they choose.
        using var colorFilter = FrameColorFilter.Create(frame.ColorOperation, frame.Red, frame.Green, frame.Blue);
        if (colorFilter is not null)
            paint.ColorFilter = colorFilter;
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

        // img is a cached, immutable SKImage owned by ThumbnailService — drawn directly, NOT
        // rebuilt from the source bitmap here. Rebuilding it per frame was a full-atlas copy +
        // GPU re-upload (67 MB/frame for a 4096² sheet), the #514 preview-framerate bottleneck.
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
        private readonly Dictionary<string, SKImage?> _cache;
        private readonly CanvasPalette                _palette;
        private readonly RollingAverage?             _drawTimes;   // non-null only when diagnostics are on

        public DrawOp(RenderSnapshot snap, Dictionary<string, SKImage?> cache, CanvasPalette palette,
            RollingAverage? drawTimes)
        {
            _snap      = snap;
            _cache     = cache;
            _palette   = palette;
            _drawTimes = drawTimes;
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

            if (_drawTimes is not null)
            {
                // Time only the Skia render — it runs on the compositor/render thread, where the
                // frame cost actually lands (the UI-thread Render() just builds the snapshot).
                var sw = System.Diagnostics.Stopwatch.StartNew();
                PreviewControl.RenderSkCore(lease.SkCanvas, _snap, _cache, _palette);
                sw.Stop();
                _drawTimes.Add(sw.Elapsed.TotalMilliseconds);
                DrawTimeOverlay.Draw(lease.SkCanvas, _drawTimes.Average);
            }
            else
            {
                PreviewControl.RenderSkCore(lease.SkCanvas, _snap, _cache, _palette);
            }
        }
    }
}



