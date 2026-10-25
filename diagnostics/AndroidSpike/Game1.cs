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

    // Builds and draws a throwaway Apos.Shapes ShapeBatch before Gum's, to split shader compile and
    // program link from the rest of ShapeRenderer.Initialize. Costs a second shader compile.
    private const bool MeasureStandaloneShapeBatch = true;

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

        // Done here, before the engine, so its cost (it builds an Apos.Shapes ShapeBatch and so
        // compiles the shader) is timed on its own. The engine skips it when already initialized.
        var sw = Stopwatch.StartNew();
        if (MeasureStandaloneShapeBatch)
        {
            var batch = new Apos.Shapes.ShapeBatch(GraphicsDevice);
            Timing.Log($"Standalone new ShapeBatch: {sw.ElapsedMilliseconds} ms");
            sw.Restart();
            batch.Begin();
            batch.FillCircle(Vector2.Zero, 10, Color.White);
            batch.End();
            Timing.Log($"Standalone ShapeBatch first draw (program link): {sw.ElapsedMilliseconds} ms");
            sw.Restart();
        }
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
