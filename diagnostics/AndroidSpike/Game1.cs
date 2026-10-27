using System.Diagnostics;
using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gum.GueDeriving;
using Gum.Renderables;

namespace AndroidSpike;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly Stopwatch _sinceActivityStart;
    private int _drawCount;
    private bool _firstUpdateLogged;

    public Game1(Stopwatch sinceActivityStart)
    {
        _sinceActivityStart = sinceActivityStart;
        _graphics = new GraphicsDeviceManager(this)
        {
            // KNI's Android backend rejects FL10_0 ("Adapter '' does not support the FL10_0 profile")
            // even on an OpenGL ES 3.2 device, so this spike uses HiDef.
            GraphicsProfile = GraphicsProfile.HiDef,
            IsFullScreen = true,
        };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
        Timing.Log($"Game.Initialize start, {_sinceActivityStart.ElapsedMilliseconds} ms after OnCreate");

        // Done before the engine so its cost is timed on its own. The engine skips it when already initialized.
        var sw = Stopwatch.StartNew();
        ShapeRenderer.Self.Initialize(GraphicsDevice, Content);
        Timing.Log($"Gum ShapeRenderer.Initialize: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        FlatRedBallService.Default.Initialize<GameScreen>(this);
        Timing.Log($"FlatRedBallService.Initialize: {sw.ElapsedMilliseconds} ms");
    }

    protected override void Update(GameTime gameTime)
    {
        FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
        if (!_firstUpdateLogged)
        {
            _firstUpdateLogged = true;
            Timing.Log($"First Update done, {_sinceActivityStart.ElapsedMilliseconds} ms after OnCreate");
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        // The engine's ShapesBatch creates its ShapeBatch lazily on the first shape draw, and KNI
        // links shader programs at first draw, so the first Draw carries those costs.
        var sw = Stopwatch.StartNew();
        FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
        _drawCount++;
        if (_drawCount <= 2)
            Timing.Log($"Draw #{_drawCount}: {sw.ElapsedMilliseconds} ms, {_sinceActivityStart.ElapsedMilliseconds} ms after OnCreate");
    }
}

static class Timing
{
    public static void Log(string message) => Android.Util.Log.Info("FRBSPIKE", message);
}
