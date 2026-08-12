using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlatRedBall2.Tests;
using Microsoft.Xna.Framework;
using RenderingLibrary;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Automation;

/// <summary>
/// Drives automation mode through <see cref="FlatRedBallService.Update"/>/<see cref="FlatRedBallService.Draw"/>
/// as a real <see cref="Game"/> calls them, instead of calling <see cref="global::FlatRedBall2.Automation.AutomationMode"/>
/// methods directly the way <c>AutomationModeTests.cs</c> does. Those tests pass even if the engine's
/// wiring is wrong, because they never go through <c>Update</c>/<c>Draw</c> at all. This covers the
/// wiring: step responses must flush from <c>Draw</c> (not <c>Update</c>), and an armed screenshot must
/// hold off <c>Game.SuppressDraw</c> so it isn't starved when no further step follows it. See issue #836.
/// </summary>
[Collection(GraphicsDeviceCollection.Name)]
public class AutomationModeEngineWiringTests
{
    private static bool GumIsOwnedElsewhere => SystemManagers.Default is not null;

    // Update/Draw call through to the engine, matching how a real Game1 wires FlatRedBallService.
    // Engine is null during the throwaway RunOneFrame() TryCreateGame uses to force device creation.
    private class WiredGame : Game
    {
        public FlatRedBallService? Engine;
        protected override void Update(GameTime gameTime) => Engine?.Update(gameTime);
        protected override void Draw(GameTime gameTime) => Engine?.Draw();
    }

    private static WiredGame? TryCreateGame()
    {
        try
        {
            var game = new WiredGame();
            _ = new GraphicsDeviceManager(game) { PreferredBackBufferWidth = 64, PreferredBackBufferHeight = 64 };
            game.RunOneFrame();
            return game;
        }
        catch (Exception e)
        {
            // No display, no driver, or a headless agent — same contract as GraphicsDeviceFixture.
            System.Diagnostics.Debug.WriteLine($"[tests] No graphics device available: {e.Message}");
            return null;
        }
    }

    [Fact]
    public void StepCountThenScreenshot_ThroughRealGameTicks_FlushesFromDrawAndCapturesWithoutAStep()
    {
        if (GumIsOwnedElsewhere)
            return;

        using var game = TryCreateGame();
        if (game is null)
            return;

        var engine = new FlatRedBallService();
        engine.Initialize(game);
        engine.Start<Screen>();
        game.Engine = engine;

        var screenshotPath = Path.Combine(Path.GetTempPath(), $"frb2-automation-{Guid.NewGuid():N}.png");
        var output = new StringWriter();
        try
        {
            // No "step" after record_next_screenshot: fulfilling it depends entirely on the engine
            // holding off SuppressDraw on the tick that arms it, since nothing else grants a frame.
            var commands =
                "{\"cmd\":\"step\",\"count\":3}\n" +
                $"{{\"cmd\":\"record_next_screenshot\",\"path\":{JsonSerializer.Serialize(screenshotPath)}}}\n";
            engine.StartAutomationMode(seed: 0, input: new StringReader(commands), output: output);

            for (int i = 0; i < 100 && !File.Exists(screenshotPath); i++)
            {
                game.RunOneFrame();
                if (!File.Exists(screenshotPath))
                    System.Threading.Thread.Sleep(5);
            }

            File.Exists(screenshotPath).ShouldBeTrue("timed out waiting for the automation-mode screenshot to be captured");

            var lines = output.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => JsonDocument.Parse(l).RootElement)
                .ToList();

            lines.Count.ShouldBe(4, $"expected 3 step responses + 1 screenshot response, got:\n{output}");

            var stepResponses = lines.Take(3).ToList();
            foreach (var response in stepResponses)
                response.GetProperty("ok").GetBoolean().ShouldBeTrue();
            var frames = stepResponses.Select(r => r.GetProperty("frame").GetInt64()).ToArray();
            frames.ShouldBe(frames.OrderBy(f => f).ToArray()); // strictly increasing, one per Draw

            var screenshotResponse = lines[3];
            screenshotResponse.GetProperty("ok").GetBoolean().ShouldBeTrue();
            screenshotResponse.GetProperty("result").GetProperty("path").GetString().ShouldBe(screenshotPath);
        }
        finally
        {
            if (File.Exists(screenshotPath))
                File.Delete(screenshotPath);
            engine.Shutdown();
        }
    }
}
