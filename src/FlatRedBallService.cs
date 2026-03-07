using System;
using System.Collections.Generic;
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

public class FlatRedBallService
{
    public static FlatRedBallService Default { get; } = new FlatRedBallService();

    private Game? _game;
    private GraphicsDeviceManager? _graphicsManager;
    private SpriteBatch? _spriteBatch;
    private Action? _pendingScreenChange;
    private readonly List<GraphicalUiElement> _gumUpdateList = new();
    private readonly GameSynchronizationContext _syncContext = new();
    private readonly GumService _gum = new GumService();

    public FlatRedBallService() { }

    public void Initialize(Game game, EngineInitSettings? settings = null)
    {
        _game = game;
        _graphicsManager = game.Services.GetService(typeof(IGraphicsDeviceManager)) as GraphicsDeviceManager;
        _spriteBatch = new SpriteBatch(game.GraphicsDevice);
        SynchronizationContext.SetSynchronizationContext(_syncContext);
        ContentManager.Initialize(game.Content, game.GraphicsDevice);
        ShapesBatch.Instance.Initialize(game.GraphicsDevice, game.Content);

        var bounds = game.Window.ClientBounds;
        ApplyCameraSettings(Camera, bounds.Width, bounds.Height);
        InputManager.SetCamera(Camera);

        game.Window.ClientSizeChanged += HandleClientSizeChanged;

        if (settings?.GumProjectFile is string gumProjectFile)
        {
            _gum.Initialize(game, gumProjectFile);
            _gum.LoadAnimations();
        }
        else
        {
            _gum.Initialize(game, DefaultVisualsVersion.V3);
        }
        GumRenderBatch.Instance.Initialize();
    }

    // Screen management
    public Screen CurrentScreen { get; private set; } = new Screen();

    /// <param name="configure">
    /// Optional callback invoked on the new screen instance before <see cref="Screen.CustomInitialize"/> runs.
    /// Use this to set public properties that <c>CustomInitialize</c> depends on.
    /// </param>
    public void Start<T>(Action<T>? configure = null) where T : Screen, new()
    {
        var screen = new T();
        configure?.Invoke(screen);
        ActivateScreen(screen, applyWindowSettings: true);
    }

    internal void RequestScreenChange<T>(Action<T>? configure = null) where T : Screen, new()
    {
        _pendingScreenChange = () =>
        {
            CurrentScreen.CustomDestroy();
            CurrentScreen.ContentManager.UnloadAll();

            // Cancel all async work that was started on the old screen.
            // ClearTasks cancels pending delay/predicate tasks (triggering TaskCanceledException
            // in any awaiting code); Clear discards stale continuations from the sync context queue.
            CurrentScreen._cts.Cancel();
            TimeManager.ClearTasks();
            _syncContext.Clear();

            var screen = new T();
            configure?.Invoke(screen);
            ActivateScreen(screen, applyWindowSettings: false);
        };
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
            ApplyWindowSettings(pref ?? DisplaySettings);

        screen.Engine = this;
        // Each screen gets its own ContentManager so UnloadAll() only disposes that screen's
        // assets without touching engine-level content (e.g., the Apos.Shapes shader effect).
        screen.ContentManager.Initialize(new ContentManager(_game!.Services, _game!.Content.RootDirectory), _game!.GraphicsDevice);

        var bounds = _game!.Window.ClientBounds;
        ApplyCameraSettings(screen.Camera, bounds.Width, bounds.Height);
        InputManager.SetCamera(screen.Camera);
        TimeManager.ResetScreen();

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
            if (_game != null)
                _game.Window.Position = Point.Zero;
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

                // Re-center the window. When entering fullscreen we set Position = (0,0);
                // without a reset the title bar stays above the visible screen area and the
                // window appears borderless even though it is not.
                var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                int cx = (display.Width  - _graphicsManager.PreferredBackBufferWidth)  / 2;
                int cy = (display.Height - _graphicsManager.PreferredBackBufferHeight) / 2;
                _game.Window.Position = new Point(System.Math.Max(0, cx), System.Math.Max(30, cy));
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
    public GraphicsDevice GraphicsDevice => _game!.GraphicsDevice;
    public GameRandom Random { get; } = new GameRandom();
    public InputManager InputManager { get; } = new InputManager();
    public AudioManager AudioManager { get; } = new AudioManager();
    public ContentManagerService ContentManager { get; } = new ContentManagerService();
    public TimeManager TimeManager { get; } = new TimeManager();
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

    public void Update(GameTime gameTime)
    {
        // Apply pending screen transition at start of frame
        if (_pendingScreenChange != null)
        {
            var change = _pendingScreenChange;
            _pendingScreenChange = null;
            change();
        }

        TimeManager.Update(gameTime);
        CurrentScreen.Overlay.BeginFrame();
        InputManager.Update();

        // Route input events (click, hover, etc.) to all active Gum elements.
        // _gum.Root covers anything added via AddToRoot();
        // screen GumRenderables cover elements added via screen.Add().
        _gumUpdateList.Clear();
        _gumUpdateList.Add(_gum.Root);
        foreach (var r in CurrentScreen.GumRenderables)
            _gumUpdateList.Add(r.Visual);
        _gum.Update(gameTime, _gumUpdateList);

        // Complete any delay tasks whose conditions are now met, then flush their
        // continuations onto the game thread. This runs before CustomActivity so
        // screen/entity code sees the results of completed tasks in the same frame.
        TimeManager.DoTaskLogic();
        _syncContext.Update();

        CurrentScreen.Update(TimeManager.CurrentFrameTime);
    }

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
