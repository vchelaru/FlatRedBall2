using Beefball.Screens;
using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Beefball;

/// <summary>
/// "--screenshots dir" mode: walks the game through a fixed script, saves the real back buffer at the
/// marked steps, then exits. The script counts frames, not wall time, and pauses while a capture is
/// waiting for its draw, so a slow start-up cannot skip a shot.
/// </summary>
public sealed class ScreenshotDriver
{
    private readonly record struct Step(int WaitFrames, Action<FlatRedBallService, Game>? Do, string? Shot);

    private readonly string _directory;
    private readonly Queue<Step> _steps = new();
    private int _wait = -1;
    private string? _pendingName;

    public ScreenshotDriver(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        Step Act(int wait, Action<FlatRedBallService, Game> action) => new(wait, action, null);
        Step Shot(int wait, string name) => new(wait, null, name);
        static GameScreen Gs(FlatRedBallService e) => (GameScreen)e.CurrentScreen;

        _steps.Enqueue(Shot(45, "01_title"));
        _steps.Enqueue(Act(1, (e, _) => e.CurrentScreen.MoveToScreen<GameScreen>()));
        _steps.Enqueue(Act(10, (e, _) => Gs(e).StageRally()));
        _steps.Enqueue(Shot(8, "02_rally_trail"));
        _steps.Enqueue(Shot(20, "03_rally_hit"));
        _steps.Enqueue(Shot(5, "03b_rally_after"));
        // Glow is Gum content in world space, so it has to follow camera zoom and a bigger window.
        _steps.Enqueue(Act(5, (e, _) => Gs(e).Camera.Zoom = 1.6f));
        _steps.Enqueue(Shot(4, "07_zoom_1_6"));
        _steps.Enqueue(Act(2, (e, _) => { Gs(e).Camera.Zoom = 1f; e.ApplyWindowSettings(new FlatRedBall2.Rendering.DisplaySettings { PreferredWindowWidth = 1500, PreferredWindowHeight = 1110 }); }));
        _steps.Enqueue(Shot(6, "08_window_1500x1110"));
        _steps.Enqueue(Act(2, (e, _) => e.ApplyWindowSettings(new FlatRedBall2.Rendering.DisplaySettings { PreferredWindowWidth = Arena.ResolutionWidth, PreferredWindowHeight = Arena.ResolutionHeight })));
        _steps.Enqueue(Act(60, (e, _) => { Gs(e).StageScores(2, 1); Gs(e).StageGoal(1); }));
        _steps.Enqueue(Shot(18, "04_goal_pop"));
        _steps.Enqueue(Shot(40, "05_goal_settled"));
        _steps.Enqueue(Act(60, (e, _) => { Gs(e).StageScores(1, 4); Gs(e).StageGoal(2); }));
        _steps.Enqueue(Shot(130, "06_win"));
        _steps.Enqueue(Act(2, (_, g) => g.Exit()));
    }

    public void Update(FlatRedBallService engine, Game game)
    {
        if (_pendingName != null || _steps.Count == 0) return;

        if (_wait < 0) _wait = _steps.Peek().WaitFrames;
        if (_wait-- > 0) return;

        var step = _steps.Dequeue();
        step.Do?.Invoke(engine, game);
        _pendingName = step.Shot;
        _wait = -1;
    }

    public void AfterDraw(GraphicsDevice device)
    {
        if (_pendingName == null) return;
        var pp = device.PresentationParameters;
        int w = pp.BackBufferWidth, h = pp.BackBufferHeight;
        var pixels = new Color[w * h];
        device.GetBackBufferData(pixels);
        using var texture = new Texture2D(device, w, h);
        texture.SetData(pixels);
        using var stream = File.Create(Path.Combine(_directory, _pendingName + ".png"));
        texture.SaveAsPng(stream, w, h);
        Console.WriteLine($"saved {_pendingName}.png ({w}x{h})");
        _pendingName = null;
    }
}
