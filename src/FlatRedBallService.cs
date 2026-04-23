using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Audio;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.UI;
using FlatRedBall2.Input;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using FlatRedBall2.Utilities;
using Gum.Forms;
using Gum.Wireframe;
using MonoGameGum;
using Microsoft.Xna.Framework.Content;

namespace FlatRedBall2;

/// <summary>
/// The engine root. Owns the <see cref="CurrentScreen"/>, the per-engine subsystems
/// (<see cref="Input"/>, <see cref="Audio"/>, <see cref="Content"/>, <see cref="Time"/>,
/// <see cref="Random"/>, <see cref="RenderDiagnostics"/>), and the integration with the
/// MonoGame <see cref="Microsoft.Xna.Framework.Game"/> loop.
/// <para>
/// Most games access the engine through <see cref="Default"/>, the single static instance,
/// and call <see cref="Initialize"/>, <see cref="Update"/>, and <see cref="Draw"/> from the
/// matching <c>Game</c> hooks.
/// </para>
/// </summary>
public class FlatRedBallService
{
    /// <summary>The shared engine instance used by every screen, entity, and factory.</summary>
    public static FlatRedBallService Default { get; } = new FlatRedBallService();

    private Game? _game;
    private GraphicsDeviceManager? _graphicsManager;
    private SpriteBatch? _spriteBatch;
    private Action? _pendingScreenChange;
    private Type? _lastScreenType;
    private Action<Screen>? _lastScreenConfigure;
    private readonly List<GraphicalUiElement> _gumUpdateList = new();
    private float _lastGumCanvasWidth;
    private float _lastGumCanvasHeight;
    private readonly GameSynchronizationContext _syncContext = new();
    private readonly GumService _gum = new GumService();

    /// <summary>
    /// Constructs an engine instance and auto-detects <see cref="SourceContentRoot"/>. Most
    /// games use <see cref="Default"/> rather than constructing their own — multi-instance
    /// engines are only useful for advanced testing scenarios.
    /// </summary>
    public FlatRedBallService()
    {
        SourceContentRoot = DetectSourceContentRoot(AppContext.BaseDirectory);
        OutputContentRoot = AppContext.BaseDirectory;
    }

    /// <summary>
    /// Absolute path to the project's source content folder, used by <see cref="Screen.WatchContent(string, Action, string?)"/>
    /// and <see cref="Screen.WatchContentDirectory(string, Action{string}, string?)"/> to locate
    /// the file the user actually edits (vs the copy MSBuild dropped into the build output).
    /// <para>
    /// Auto-detected at construction by walking up from <see cref="AppContext.BaseDirectory"/>
    /// looking for a <c>.csproj</c>; in a shipping build this returns <c>null</c> and the
    /// <c>WatchContent</c>* overloads silently no-op. Override this if auto-detection picks the
    /// wrong root (e.g. unusual project layouts, multi-project repos).
    /// </para>
    /// </summary>
    public string? SourceContentRoot { get; set; }

    /// <summary>
    /// Absolute path to the build-output folder where copied content lives at runtime. Defaults
    /// to <see cref="AppContext.BaseDirectory"/>. Override only if your build pipeline writes
    /// content to a directory other than the executable's folder.
    /// </summary>
    public string OutputContentRoot { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a <c>.csproj</c>. Returns the
    /// directory containing the first match, or <c>null</c> if none found within ~10 levels.
    /// Public for testing.
    /// </summary>
    public static string? DetectSourceContentRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        for (int i = 0; i < 10 && dir != null; i++)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// The MonoGame <see cref="Microsoft.Xna.Framework.Game"/> instance passed to <see cref="Initialize"/>.
    /// Use this to call <see cref="Microsoft.Xna.Framework.Game.Exit"/> or access window/graphics properties.
    /// Throws if accessed before <see cref="Initialize"/> is called.
    /// </summary>
    public Game Game => _game ?? throw new InvalidOperationException(
        "FlatRedBallService has not been initialized. Call Initialize() first.");

    /// <summary>
    /// Initializes the engine. Call this inside <c>Game.Initialize</c>, after <c>base.Initialize()</c>.
    /// </summary>
    /// <remarks>
    /// Does not modify <c>Game.IsMouseVisible</c>. Set <c>IsMouseVisible = true</c> in the
    /// <c>Game1</c> constructor before calling this if the game uses mouse or cursor input —
    /// MonoGame defaults the property to <c>false</c>.
    /// </remarks>
    public void Initialize(Game game, EngineInitSettings? settings = null)
    {
        _game = game;
        _graphicsManager = game.Services.GetService(typeof(IGraphicsDeviceManager)) as GraphicsDeviceManager;
        _spriteBatch = new SpriteBatch(game.GraphicsDevice);
        SynchronizationContext.SetSynchronizationContext(_syncContext);
        Content.Initialize(game.Content, game.GraphicsDevice);
        ShapesBatch.Instance.Initialize(game.GraphicsDevice, game.Content);

        var bounds = game.Window.ClientBounds;
        ApplyCameraSettings(Camera, bounds.Width, bounds.Height);
        Input.SetCamera(Camera);

        game.Window.ClientSizeChanged += HandleClientSizeChanged;

        if (settings?.GumProjectFile is string gumProjectFile)
        {
            _gum.Initialize(game, gumProjectFile);
#pragma warning disable CS0618 // Gum marks this as obsolete, but it's just because it's still experimental. It's okay.
            _gum.LoadAnimations();
#pragma warning restore CS0618 // Type or member is obsolete
        }
        else
        {
            _gum.Initialize(game, DefaultVisualsVersion.V3);
        }
        GumRenderBatch.Instance.Initialize();
    }

    // Screen management
    /// <summary>
    /// The screen currently being updated and drawn. Replaced by <see cref="Start{T}"/> /
    /// <c>RequestScreenChange</c> at the next frame boundary — never mid-frame.
    /// </summary>
    public Screen CurrentScreen { get; private set; } = new Screen();

    /// <param name="configure">
    /// Optional callback invoked on the new screen instance before <see cref="Screen.CustomInitialize"/> runs.
    /// Use this to set public properties that <c>CustomInitialize</c> depends on.
    /// <para>
    /// <b>Avoid closing over mutable locals here.</b> The engine retains this callback to replay it
    /// on <see cref="Screen.RestartScreen"/>; mutating a captured local after this call changes what
    /// restart sees. Pass values directly rather than via captured locals.
    /// </para>
    /// </param>
    public void Start<T>(Action<T>? configure = null) where T : Screen, new()
    {
        var screen = new T();
        _lastScreenType = typeof(T);
        _lastScreenConfigure = configure == null ? null : s => configure((T)s);
        _lastScreenConfigure?.Invoke(screen);
        ActivateScreen(screen, applyWindowSettings: true);
    }

    internal void RequestScreenChange<T>(Action<T>? configure = null) where T : Screen, new()
    {
        _pendingScreenChange = () =>
        {
            TeardownCurrentScreen();

            var screen = new T();
            _lastScreenType = typeof(T);
            _lastScreenConfigure = configure == null ? null : s => configure((T)s);
            _lastScreenConfigure?.Invoke(screen);
            ActivateScreen(screen, applyWindowSettings: false);
        };
    }

    internal void RequestScreenRestart(Action<Screen>? newConfigure, RestartMode mode)
    {
        _pendingScreenChange = () =>
        {
            HotReloadState? state = null;
            if (mode == RestartMode.HotReload)
            {
                state = new HotReloadState();
                CurrentScreen.SaveHotReloadState(state);
            }

            TeardownCurrentScreen();

            // If a new configure was supplied, it REPLACES the retained one — both for this
            // restart and for any future restart that doesn't supply its own. There is one
            // configure slot on the engine; whoever set it last wins.
            if (newConfigure != null)
                _lastScreenConfigure = newConfigure;

            var screen = (Screen)Activator.CreateInstance(_lastScreenType!)!;
            _lastScreenConfigure?.Invoke(screen);
            ActivateScreen(screen, applyWindowSettings: false);

            if (state != null)
                screen.RestoreHotReloadState(state);
        };
    }

    private void TeardownCurrentScreen()
    {
        CurrentScreen.DisposeContentWatchers();
        CurrentScreen.CustomDestroy();
        CurrentScreen._tweens.Clear();
        CurrentScreen.ContentManager.UnloadAll();

        // Cancel all async work that was started on the old screen.
        // ClearTasks cancels pending delay/predicate tasks (triggering TaskCanceledException
        // in any awaiting code); Clear discards stale continuations from the sync context queue.
        CurrentScreen._cts.Cancel();
        Time.ClearTasks();
        _syncContext.Clear();
    }

    private void ActivateScreen(Screen screen, bool applyWindowSettings)
    {
        foreach (var factory in _factories.Values)
            factory.DestroyAll();
        _factories.Clear();

        // Clear any Gum elements left over from the previous screen.
        // This covers controls added via AddToRoot() as well as screen-specific GumRenderables,
        // which are abandoned with the old Screen object.
        _gum.Root.Children.Clear();

        // Apply the screen's preferred display settings. Camera properties always apply;
        // window properties (size, resizing) only apply on Start to avoid mid-game window pops.
        var pref = screen.PreferredDisplaySettings;
        if (pref != null)
            ApplyCameraSettingsFrom(pref);

        if (applyWindowSettings)
        {
            // When the host opts into Window.AllowUserResizing, the back buffer is being managed
            // externally (e.g. KNI BlazorGL tracks the canvas DOM size). Forcing PreferredBackBuffer
            // here would clamp the back buffer to the engine's design resolution while the canvas
            // stays at its DOM size, producing a coordinate-space mismatch in Camera.ScreenToWorld.
            // If the screen has explicit display settings, those still win.
            bool externallyManaged = _game?.Window.AllowUserResizing == true && pref == null;
            if (!externallyManaged)
                ApplyWindowSettings(pref ?? DisplaySettings);
        }

        screen.Engine = this;
        if (_game != null)
        {
            // Each screen gets its own ContentManager so UnloadAll() only disposes that screen's
            // assets without touching engine-level content (e.g., the Apos.Shapes shader effect).
            screen.ContentManager.Initialize(new ContentManager(_game.Services, _game.Content.RootDirectory), _game.GraphicsDevice);

            var bounds = _game.Window.ClientBounds;
            ApplyCameraSettings(screen.Camera, bounds.Width, bounds.Height);
        }
        Input.SetCamera(screen.Camera);
        Time.ResetScreen();

        CurrentScreen = screen;
        screen.CustomInitialize();
    }

    private void ApplyCameraSettingsFrom(DisplaySettings source)
    {
        DisplaySettings.Zoom = source.Zoom;
        DisplaySettings.ResizeMode = source.ResizeMode;
        DisplaySettings.FixedAspectRatio = source.FixedAspectRatio;
        DisplaySettings.ResolutionWidth = source.ResolutionWidth;
        DisplaySettings.ResolutionHeight = source.ResolutionHeight;
        DisplaySettings.LetterboxColor = source.LetterboxColor;
        DisplaySettings.WindowMode = source.WindowMode;
    }

    /// <summary>
    /// Applies window settings immediately at runtime. Safe to call at any time — not just at startup.
    /// <para>
    /// To toggle fullscreen: pass <see cref="DisplaySettings"/> with <see cref="DisplaySettings.WindowMode"/>
    /// set to <see cref="Rendering.WindowMode.FullscreenBorderless"/> or <see cref="Rendering.WindowMode.Windowed"/>.
    /// </para>
    /// <para>
    /// To apply windowed-only changes (size, resizing) without touching fullscreen state, pass
    /// <see cref="Rendering.WindowMode.Windowed"/> with the desired <see cref="DisplaySettings.PreferredWindowWidth"/>
    /// and <see cref="DisplaySettings.PreferredWindowHeight"/>.
    /// </para>
    /// </summary>
    public void ApplyWindowSettings(DisplaySettings source)
    {
        if (_graphicsManager == null) return;

        if (source.WindowMode == Rendering.WindowMode.FullscreenBorderless)
        {
            _graphicsManager.HardwareModeSwitch = false;
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphicsManager.PreferredBackBufferWidth  = mode.Width;
            _graphicsManager.PreferredBackBufferHeight = mode.Height;
            _graphicsManager.IsFullScreen = true;
            _graphicsManager.ApplyChanges();
#if !KNI
            if (_game != null)
                _game.Window.Position = Point.Zero;
#endif
        }
        else
        {
            _graphicsManager.IsFullScreen = false;

            if (source.PreferredWindowWidth.HasValue)
            {
                _graphicsManager.PreferredBackBufferWidth  = source.PreferredWindowWidth.Value;
                _graphicsManager.PreferredBackBufferHeight = source.PreferredWindowHeight!.Value;
            }
            else
            {
                // No explicit size requested — restore to the design resolution so the window
                // doesn't remain at the native fullscreen back-buffer size (which would overflow
                // onto other monitors or appear borderless at full screen size).
                _graphicsManager.PreferredBackBufferWidth  = DisplaySettings.ResolutionWidth;
                _graphicsManager.PreferredBackBufferHeight = DisplaySettings.ResolutionHeight;
            }

            _graphicsManager.ApplyChanges();

            if (_game != null)
            {
                _game.Window.AllowUserResizing = source.AllowUserResizing;

#if !KNI
                // Re-center the window. When entering fullscreen we set Position = (0,0);
                // without a reset the title bar stays above the visible screen area and the
                // window appears borderless even though it is not.
                var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                int cx = (display.Width  - _graphicsManager.PreferredBackBufferWidth)  / 2;
                int cy = (display.Height - _graphicsManager.PreferredBackBufferHeight) / 2;
                _game.Window.Position = new Point(System.Math.Max(0, cx), System.Math.Max(30, cy));
#endif
            }
        }

        DisplaySettings.WindowMode = source.WindowMode;
    }

    // Factory registry — populated automatically when a Factory<T> is constructed
    private readonly Dictionary<Type, IFactory> _factories = new();

    /// <summary>Registers a factory so entities can retrieve it via <see cref="GetFactory{T}"/>.</summary>
    /// <remarks>Called automatically by <see cref="Factory{T}"/>; you should not need to call this directly.</remarks>
    public void RegisterFactory<T>(Factory<T> factory) where T : Entity, new()
        => _factories[typeof(T)] = factory;

    /// <summary>Returns the factory registered for <typeparamref name="T"/>.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no factory for <typeparamref name="T"/> has been created yet.</exception>
    public Factory<T> GetFactory<T>() where T : Entity, new()
    {
        if (_factories.TryGetValue(typeof(T), out var factory))
            return (Factory<T>)factory;
        throw new InvalidOperationException(
            $"No factory registered for {typeof(T).Name}. Create a Factory<{typeof(T).Name}> in CustomInitialize before calling GetFactory.");
    }

    internal void SortPartitionedFactories()
    {
        foreach (var factory in _factories.Values)
            factory.SortForPartition();
    }

    /// <summary>
    /// The engine's default display configuration. Applied to every screen that does not declare
    /// its own <see cref="Screen.PreferredDisplaySettings"/>.
    /// Set camera properties here once at startup; they carry through every screen transition.
    /// Window properties (<see cref="DisplaySettings.PreferredWindowWidth"/> etc.) on this instance
    /// are applied by <see cref="Start{T}"/> when no per-screen override exists.
    /// </summary>
    public DisplaySettings DisplaySettings { get; } = new DisplaySettings();

    /// <summary>
    /// Configures the <see cref="GraphicsDeviceManager"/> with the starting screen's preferred window
    /// settings <em>before</em> <c>base.Initialize()</c> is called, so the window appears at the
    /// correct size (or in fullscreen) without any visible flicker on startup.
    /// Handles both <see cref="Rendering.WindowMode.Windowed"/> and
    /// <see cref="Rendering.WindowMode.FullscreenBorderless"/>.
    /// Call this from <c>Game1</c>'s constructor, passing the same screen type you will pass to
    /// <see cref="Start{T}"/>.
    /// <para>
    /// On browser hosts (KNI BlazorGL), set <c>Window.AllowUserResizing = true</c> in the
    /// <c>Game</c> constructor before calling this. The browser canvas dictates the back-buffer
    /// size, and that flag tells the engine to defer to it instead of clamping to a design
    /// resolution — clamping would offset cursor coordinates relative to the displayed canvas.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// public Game1()
    /// {
    ///     _graphics = new GraphicsDeviceManager(this);
    ///     FlatRedBallService.Default.PrepareWindow&lt;MyStartScreen&gt;(_graphics);
    /// }
    /// </code>
    /// </example>
    public void PrepareWindow<T>(GraphicsDeviceManager graphics) where T : Screen, new()
    {
        var settings = new T().PreferredDisplaySettings ?? DisplaySettings;
        if (settings.WindowMode == Rendering.WindowMode.FullscreenBorderless)
        {
            graphics.HardwareModeSwitch = false;
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            graphics.PreferredBackBufferWidth  = mode.Width;
            graphics.PreferredBackBufferHeight = mode.Height;
            graphics.IsFullScreen = true;
        }
        else if (settings.PreferredWindowWidth.HasValue)
        {
            graphics.PreferredBackBufferWidth  = settings.PreferredWindowWidth.Value;
            graphics.PreferredBackBufferHeight = settings.PreferredWindowHeight!.Value;
        }
    }

    private void ApplyCameraSettings(Camera camera, int windowWidth, int windowHeight)
    {
        var dest = DisplaySettings.ComputeDestinationViewport(windowWidth, windowHeight);
        camera.SetViewport(dest);
        camera.Zoom = DisplaySettings.Zoom;

        if (DisplaySettings.ResizeMode == Rendering.ResizeMode.IncreaseVisibleArea)
        {
            // TargetWidth tracks the viewport so scale = Zoom at all window sizes.
            // Visible world = vpW / Zoom — grows as the window grows.
            camera.TargetWidth = dest.Width;
            camera.TargetHeight = dest.Height;
        }
        else
        {
            // Height-dominant: fix the vertical world extent to ResolutionHeight and derive
            // TargetWidth so that both axes use the same height-based scale. This prevents
            // non-uniform stretching when the window aspect ratio differs from the design resolution.
            camera.TargetHeight = DisplaySettings.ResolutionHeight;
            camera.TargetWidth = dest.Height > 0
                ? (int)(dest.Width * DisplaySettings.ResolutionHeight / (float)dest.Height)
                : DisplaySettings.ResolutionWidth;
        }
    }

    private void HandleClientSizeChanged(object? sender, EventArgs e)
    {
        var bounds = _game!.Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var dest = DisplaySettings.ComputeDestinationViewport(bounds.Width, bounds.Height);
        var camera = CurrentScreen.Camera;
        camera.SetViewport(dest);

        if (DisplaySettings.ResizeMode == Rendering.ResizeMode.IncreaseVisibleArea)
        {
            camera.TargetWidth = dest.Width;
            camera.TargetHeight = dest.Height;
        }
        else
        {
            // StretchVisibleArea: height-dominant — TargetHeight is fixed, TargetWidth tracks
            // the viewport width so the height-based scale stays uniform on resize.
            camera.TargetHeight = DisplaySettings.ResolutionHeight;
            camera.TargetWidth = dest.Height > 0
                ? (int)(dest.Width * DisplaySettings.ResolutionHeight / (float)dest.Height)
                : DisplaySettings.ResolutionWidth;
        }
    }

    // Sub-systems
    /// <summary>The MonoGame graphics device. Throws if accessed before <see cref="Initialize"/>.</summary>
    public GraphicsDevice GraphicsDevice => _game!.GraphicsDevice;
    /// <summary>Deterministic random number source — used by every gameplay system that needs randomness so seeds can be reproduced.</summary>
    public GameRandom Random { get; } = new GameRandom();
    /// <summary>Polled keyboard, mouse, and gamepad state. Updated once per frame at the top of <see cref="Update"/>.</summary>
    public InputManager Input { get; } = new InputManager();
    /// <summary>Sound effect and music playback service.</summary>
    public AudioManager Audio { get; } = new AudioManager();
    /// <summary>The active screen's content loader. Auto-recreated each screen change.</summary>
    public ContentManagerService Content { get; } = new ContentManagerService();
    /// <summary>Engine clocks and async delay primitives. See <see cref="TimeManager"/>.</summary>
    public TimeManager Time { get; } = new TimeManager();
    /// <summary>Per-frame draw-call instrumentation. Off by default — see <see cref="Diagnostics.RenderDiagnostics.IsEnabled"/>.</summary>
    public RenderDiagnostics RenderDiagnostics { get; } = new RenderDiagnostics();

    /// <summary>
    /// The Gum UI service owned by this engine instance. Use this to access the root element,
    /// load Gum projects, or configure themes.
    /// </summary>
    public GumService Gum => _gum;

    /// <summary>The active screen's camera. Shortcut for <see cref="CurrentScreen"/>.<see cref="Screen.Camera"/>.</summary>
    public Camera Camera => CurrentScreen.Camera;

    /// <summary>The active screen's overlay. Shortcut for <see cref="CurrentScreen"/>.<see cref="Screen.Overlay"/>.</summary>
    public Overlay Overlay => CurrentScreen.Overlay;

    /// <summary>
    /// Per-frame engine tick. Call from <c>Game.Update</c>. Drives screen transitions, input
    /// polling, content hot-reload, time accumulation, async continuations, and the active
    /// screen's <see cref="Screen.CustomActivity"/> in that order.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        // Apply pending screen transition at start of frame
        if (_pendingScreenChange != null)
        {
            var change = _pendingScreenChange;
            _pendingScreenChange = null;
            change();
        }

        // Drain any pending content reloads BEFORE entity / collision / activity passes so the
        // reloaded content (configs, textures, etc.) is in place for the rest of the frame.
        CurrentScreen.TickContentWatchers(DateTime.UtcNow);

        Time.Update(gameTime, CurrentScreen.IsPaused);
        Input.Update();

        if (_spriteBatch != null)
        {
            Audio.Update();
            CurrentScreen.Overlay.BeginFrame();

            // Keep the Gum canvas in sync with the current viewport so that UI layout
            // (percent-of-parent, anchoring, XUnits like PixelsFromCenterX) resolves to the
            // correct screen dimensions rather than the stale project defaults.
            var viewport = CurrentScreen.Camera.Viewport;
            var zoom = CurrentScreen.Camera.Zoom;
            _gum.CanvasWidth = viewport.Width / zoom;
            _gum.CanvasHeight = viewport.Height / zoom;

            // Route input events (click, hover, etc.) to all active Gum elements.
            // _gum.Root covers anything added via AddToRoot();
            // screen GumRenderables cover elements added via screen.Add().
            _gumUpdateList.Clear();
            _gumUpdateList.Add(_gum.Root);
            foreach (var r in CurrentScreen.GumRenderables)
                _gumUpdateList.Add(r.Visual);

            // If the canvas size changed, force a layout pass on every top-level element
            // so that percent-of-parent sizes, anchors, and center-based positions recompute
            // against the new canvas dimensions.
            if (_gum.CanvasWidth != _lastGumCanvasWidth || _gum.CanvasHeight != _lastGumCanvasHeight)
            {
                _lastGumCanvasWidth = _gum.CanvasWidth;
                _lastGumCanvasHeight = _gum.CanvasHeight;
                foreach (var element in _gumUpdateList)
                    element.UpdateLayout();
            }

            _gum.Update(gameTime, _gumUpdateList);
        }

        // Complete any delay tasks whose conditions are now met, then flush their
        // continuations onto the game thread. This runs before CustomActivity so
        // screen/entity code sees the results of completed tasks in the same frame.
        Time.DoTaskLogic();
        _syncContext.Update();

        CurrentScreen.Update(Time.CurrentFrameTime);
    }

    /// <summary>
    /// Per-frame engine draw. Call from <c>Game.Draw</c> after your own <c>GraphicsDevice.Clear</c>
    /// (the engine handles clearing only when <see cref="DisplaySettings"/>.<c>FixedAspectRatio</c>
    /// is set). Resolves the camera viewport, sorts the renderable list by Layer/Z, and dispatches
    /// to each renderable's <see cref="IRenderBatch"/>.
    /// </summary>
    public void Draw()
    {
        if (_spriteBatch == null) return;

        RenderDiagnostics.BeginFrame();

        var gd = _spriteBatch.GraphicsDevice;

        if (DisplaySettings.FixedAspectRatio.HasValue)
        {
            // Paint the letterbox/pillarbox bars by clearing the full window first.
            var pp = gd.PresentationParameters;
            gd.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
            gd.Clear(DisplaySettings.LetterboxColor);
        }

        // Always set the viewport — MonoGame does not reset it between frames, so a
        // previous screen's sub-viewport would persist into the next screen otherwise.
        gd.Viewport = CurrentScreen.Camera.Viewport;

        CurrentScreen.Draw(_spriteBatch, RenderDiagnostics);
    }
}
