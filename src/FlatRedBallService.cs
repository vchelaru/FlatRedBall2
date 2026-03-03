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
    private SpriteBatch? _spriteBatch;
    private Action? _pendingScreenChange;
    private readonly List<GraphicalUiElement> _gumUpdateList = new();
    private readonly GameSynchronizationContext _syncContext = new();

    public FlatRedBallService() { }

    public void Initialize(Game game)
    {
        _game = game;
        _spriteBatch = new SpriteBatch(game.GraphicsDevice);
        SynchronizationContext.SetSynchronizationContext(_syncContext);
        ContentManager.Initialize(game.Content);
        ShapesBatch.Instance.Initialize(game.GraphicsDevice, game.Content);

        var viewport = game.GraphicsDevice.Viewport;
        Camera.SetViewport(viewport);
        Camera.TargetWidth = viewport.Width;
        Camera.TargetHeight = viewport.Height;

        InputManager.SetCamera(Camera);

        GumService.Default.Initialize(game, DefaultVisualsVersion.V2);
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
        ActivateScreen(screen);
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
            ActivateScreen(screen);
        };
    }

    private void ActivateScreen(Screen screen)
    {
        foreach (var factory in _factories.Values)
            factory.DestroyAll();
        _factories.Clear();

        // Clear any Gum elements left over from the previous screen.
        // This covers controls added via AddToRoot() as well as screen-specific GumRenderables,
        // which are abandoned with the old Screen object.
        GumService.Default.Root.Children.Clear();

        screen.Engine = this;
        // Each screen gets its own ContentManager so UnloadAll() only disposes that screen's
        // assets without touching engine-level content (e.g., the Apos.Shapes shader effect).
        screen.ContentManager.Initialize(new ContentManager(_game!.Services, _game!.Content.RootDirectory));

        var viewport = _game!.GraphicsDevice.Viewport;
        screen.Camera.SetViewport(viewport);
        screen.Camera.TargetWidth = viewport.Width;
        screen.Camera.TargetHeight = viewport.Height;

        InputManager.SetCamera(screen.Camera);
        TimeManager.ResetScreen();

        CurrentScreen = screen;
        screen.CustomInitialize();
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

    // Sub-systems
    public GameRandom Random { get; } = new GameRandom();
    public InputManager InputManager { get; } = new InputManager();
    public AudioManager AudioManager { get; } = new AudioManager();
    public ContentManagerService ContentManager { get; } = new ContentManagerService();
    public TimeManager TimeManager { get; } = new TimeManager();
    public DebugRenderer DebugRenderer { get; } = new DebugRenderer();
    public RenderDiagnostics RenderDiagnostics { get; } = new RenderDiagnostics();

    /// <summary>The active screen's camera. Shortcut for <see cref="CurrentScreen"/>.<see cref="Screen.Camera"/>.</summary>
    public Camera Camera => CurrentScreen.Camera;

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
        InputManager.Update();

        // Route input events (click, hover, etc.) to all active Gum elements.
        // GumService.Default.Root covers anything added via AddToRoot();
        // screen GumRenderables cover elements added via AddGum().
        _gumUpdateList.Clear();
        _gumUpdateList.Add(GumService.Default.Root);
        foreach (var r in CurrentScreen.GumRenderables)
            _gumUpdateList.Add(r.Visual);
        GumService.Default.Update(gameTime, _gumUpdateList);

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
        CurrentScreen.Draw(_spriteBatch, RenderDiagnostics);
    }
}
