using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Audio;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.Input;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using FlatRedBall2.Utilities;

namespace FlatRedBall2;

public class FlatRedBallService
{
    public static FlatRedBallService Default { get; } = new FlatRedBallService();

    private Game? _game;
    private SpriteBatch? _spriteBatch;
    private Action? _pendingScreenChange;

    public FlatRedBallService() { }

    public void Initialize(Game game)
    {
        _game = game;
        _spriteBatch = new SpriteBatch(game.GraphicsDevice);
        ContentManager.Initialize(game.Content);
        ShapesBatch.Instance.Initialize(game.GraphicsDevice, game.Content);

        var viewport = game.GraphicsDevice.Viewport;
        Camera.SetViewport(viewport);
        Camera.TargetWidth = viewport.Width;
        Camera.TargetHeight = viewport.Height;

        InputManager.SetCamera(Camera);
    }

    // Screen management
    public Screen CurrentScreen { get; private set; } = new Screen();

    public void Start<T>() where T : Screen, new()
    {
        ActivateScreen(new T());
    }

    internal void RequestScreenChange<T>() where T : Screen, new()
    {
        _pendingScreenChange = () =>
        {
            CurrentScreen.CustomDestroy();
            CurrentScreen.ContentManager.UnloadAll();
            ActivateScreen(new T());
        };
    }

    private void ActivateScreen(Screen screen)
    {
        screen.Engine = this;
        screen.ContentManager.Initialize(_game!.Content);

        var viewport = _game!.GraphicsDevice.Viewport;
        screen.Camera.SetViewport(viewport);
        screen.Camera.TargetWidth = viewport.Width;
        screen.Camera.TargetHeight = viewport.Height;

        InputManager.SetCamera(screen.Camera);
        TimeManager.ResetScreen();

        CurrentScreen = screen;
        screen.CustomInitialize();
    }

    // Sub-systems
    public GameRandom Random { get; } = new GameRandom();
    public InputManager InputManager { get; } = new InputManager();
    public AudioManager AudioManager { get; } = new AudioManager();
    public ContentManagerService ContentManager { get; } = new ContentManagerService();
    public TimeManager TimeManager { get; } = new TimeManager();
    public DebugRenderer DebugRenderer { get; } = new DebugRenderer();
    public RenderDiagnostics RenderDiagnostics { get; } = new RenderDiagnostics();

    // Convenience: current screen's camera
    private Camera Camera => CurrentScreen.Camera;

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

        CurrentScreen.Update(TimeManager.CurrentFrameTime);
    }

    public void Draw()
    {
        if (_spriteBatch == null) return;

        RenderDiagnostics.BeginFrame();
        CurrentScreen.Draw(_spriteBatch, RenderDiagnostics);
    }
}
