using Beefball.Screens;
using FlatRedBall2;
using Microsoft.Xna.Framework;
using Vector2 = System.Numerics.Vector2;

namespace Beefball;

/// <summary>
/// "--selftest": boots the real game, drives the match logic with synthetic frames (no drawing),
/// prints PASS/FAIL lines and exits non-zero on failure. It needs a window because the Gum HUD
/// and goal wash cannot be created without a graphics device.
/// </summary>
public sealed class SelfTest
{
    private int _failures;
    private long _ticks;
    private FlatRedBallService _engine = null!;

    public int Run(FlatRedBallService engine)
    {
        _engine = engine;
        _engine.CurrentScreen.MoveToScreen<GameScreen>();
        Step(2);
        var gs = (GameScreen)_engine.CurrentScreen;

        Step(3);
        Check(gs.Score1 == 0 && gs.Score2 == 0, "game screen boots at 0-0 without exceptions");
        Check(gs.Player1.X == -180 && gs.Player2.X == 180 && gs.Puck.X == 0 && gs.Puck.Velocity == Vector2.Zero,
            "start positions: P1 -180, P2 +180, puck centered and still");

        gs.Player1.X = -300; gs.Player1.Y = 0; gs.Player1.VelocityX = -600;
        Step(60);
        Check(gs.Player1.X > -400, $"player cannot enter the goal pocket (x={gs.Player1.X:F1})");

        gs.Player1.X = 0; gs.Player1.Y = 250; gs.Player1.VelocityY = 600;
        Step(30);
        Check(gs.Player1.Y < 300, $"player bounces off the top wall (y={gs.Player1.Y:F1})");

        gs.Puck.X = 0; gs.Puck.Y = 280; gs.Puck.Velocity = new Vector2(0, 400);
        Step(15);
        Check(gs.Puck.Y < 300 && gs.Puck.VelocityY < 0, $"puck bounces off the top wall (y={gs.Puck.Y:F1}, vy={gs.Puck.VelocityY:F0})");

        gs.Puck.X = 380; gs.Puck.Y = 10; gs.Puck.Velocity = new Vector2(500, 0);
        Step(10);
        Check(gs.Score1 == 1 && gs.Score2 == 0 && gs.IsGoalPause, $"puck in the right goal scores for team 1 ({gs.Score1}-{gs.Score2})");
        Step(100);
        Check(!gs.IsGoalPause && gs.Puck.X == 0 && gs.Player1.X == -180 && gs.Player2.X == 180,
            "after the goal pause both players and the puck return to start");

        gs.Puck.X = -380; gs.Puck.Y = 0; gs.Puck.Velocity = new Vector2(-700, 0);
        Step(8);
        Check(gs.Score2 == 1, $"puck in the left goal scores for team 2 ({gs.Score1}-{gs.Score2})");
        Step(100);

        gs.Player1.X = -100; gs.Player1.Y = 0; gs.Player1.Velocity = new Vector2(300, 0);
        gs.Puck.X = -70; gs.Puck.Y = 0; gs.Puck.Velocity = Vector2.Zero;
        Step(3);
        Check(gs.Puck.VelocityX > 300, $"light puck leaves a player hit faster than the player (puck vx={gs.Puck.VelocityX:F0})");

        gs.StageScores(4, 2);
        gs.Puck.X = 380; gs.Puck.Y = 0; gs.Puck.Velocity = new Vector2(500, 0);
        Step(105);
        Check(gs.Score1 == 5 && gs.IsWon, $"first to 5 wins ({gs.Score1}-{gs.Score2}, won={gs.IsWon})");

        CheckHotReload(gs);

        Console.WriteLine(_failures == 0 ? "SELFTEST OK" : $"SELFTEST: {_failures} FAILED");
        return _failures;
    }

    /// <summary>Edits the source beefball.json, waits out the watcher debounce, and expects a restarted screen with new values and the old score.</summary>
    private void CheckHotReload(GameScreen before)
    {
        var root = _engine.SourceContentRoots.FirstOrDefault(r => File.Exists(Path.Combine(r, "Content", "beefball.json")));
        if (root == null)
        {
            Check(false, "hot reload: could not find the source Content/beefball.json");
            return;
        }

        var path = Path.Combine(root, "Content", "beefball.json");
        var original = File.ReadAllText(path);
        try
        {
            before.StageScores(3, 2);
            File.WriteAllText(path, original.Replace("\"radius\": 16", "\"radius\": 24"));
            for (int i = 0; i < 40 && ReferenceEquals(_engine.CurrentScreen, before); i++)
            {
                Thread.Sleep(30);
                Step(1);
            }
            Step(3);
            var after = _engine.CurrentScreen as GameScreen;
            Check(after != null && !ReferenceEquals(after, before), "hot reload: editing beefball.json restarts the game screen");
            Check(after != null && Math.Abs(after.Player1.Body.Radius - 24f) < 0.01f, $"hot reload: new player radius applied (r={after?.Player1.Body.Radius})");
            Check(after != null && after.Score1 == 3 && after.Score2 == 2, $"hot reload: score preserved ({after?.Score1}-{after?.Score2})");
        }
        finally
        {
            File.WriteAllText(path, original);
            // The watcher also copied the edit into the build output; put that back too.
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Content", "beefball.json"), original);
        }
    }

    private void Step(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            _ticks++;
            _engine.Update(new GameTime(TimeSpan.FromSeconds(_ticks / 60.0), TimeSpan.FromSeconds(1 / 60.0)));
        }
    }

    private void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what);
        if (!ok) _failures++;
    }
}
