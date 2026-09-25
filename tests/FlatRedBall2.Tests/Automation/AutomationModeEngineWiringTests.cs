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

    /// <summary>
    /// The whole point of the feature: an automation client types into a focused Gum TextBox and
    /// the control's own Text updates, with no reflection, no value setter and no synthetic click.
    /// </summary>
    /// <remarks>
    /// Needs a real device because a TextBox cannot be constructed without the Forms visual
    /// templates, and those are built by Gum's bootstrap from an embedded texture. The headless
    /// tests in AutomationTextInputTests.cs cover everything on this side of the seam; this covers
    /// the seam itself.
    /// </remarks>
    [Theory]
    [InlineData(true,  "MATCH-7F2A", "MATCH-7F2A")] // focused: text lands
    [InlineData(false, "MATCH-7F2A", "")]           // unfocused: nothing lands
    public void TextCommand_ThroughRealGameTicks_ReachesFocusedTextBoxOnly(
        bool focused, string injected, string expected)
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

        try
        {
            var textBox = new Gum.Forms.Controls.TextBox();
            engine.CurrentScreen.AddOverlay(textBox);
            textBox.IsFocused = focused;

            var commands =
                $"{{\"cmd\":\"input\",\"type\":\"text\",\"text\":{JsonSerializer.Serialize(injected)}}}\n" +
                "{\"cmd\":\"step\",\"count\":10}\n";
            engine.StartAutomationMode(seed: 0, input: new StringReader(commands), output: new StringWriter());

            for (int i = 0; i < 100 && (textBox.Text ?? "") != expected; i++)
            {
                game.RunOneFrame();
                System.Threading.Thread.Sleep(5);
            }

            (textBox.Text ?? "").ShouldBe(expected);
        }
        finally
        {
            engine.Shutdown();
        }
    }

    /// <summary>
    /// Backspace is the editing behavior realistic entry needs, and it travels as a key rather
    /// than as text.
    /// </summary>
    [Fact]
    public void BackspaceKey_ThroughRealGameTicks_DeletesExactlyOneCharacter()
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

        try
        {
            var typed = new Gum.Forms.Controls.TextBox();
            engine.CurrentScreen.AddOverlay(typed);
            typed.IsFocused = true;

            var commands =
                "{\"cmd\":\"input\",\"type\":\"text\",\"text\":\"abc\"}\n" +
                "{\"cmd\":\"step\"}\n" +
                "{\"cmd\":\"input\",\"type\":\"key\",\"key\":\"Back\",\"down\":true}\n" +
                "{\"cmd\":\"step\"}\n" +
                "{\"cmd\":\"input\",\"type\":\"key\",\"key\":\"Back\",\"down\":false}\n" +
                "{\"cmd\":\"step\",\"count\":10}\n";
            engine.StartAutomationMode(seed: 0, input: new StringReader(commands), output: new StringWriter());

            for (int i = 0; i < 100 && (typed.Text ?? "") != "ab"; i++)
            {
                game.RunOneFrame();
                System.Threading.Thread.Sleep(5);
            }

            // One down/up pair deleted exactly one character -- no key repeat.
            (typed.Text ?? "").ShouldBe("ab");
        }
        finally
        {
            engine.Shutdown();
        }
    }

    /// <summary>
    /// A cursor click reaching a Button through the engine's own Gum update: the roots list, the
    /// per-frame cursor install and Gum's dispatch, all inside real game ticks. The headless tests
    /// in AutomationGumFormsTests.cs drive Gum directly; this covers the engine wiring around it.
    /// </summary>
    [Fact]
    public void CursorCommand_ThroughRealGameTicks_ClicksOverlayButtonExactlyOnce()
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

        try
        {
            // Fills the overlay, so the click lands however the 64x64 window maps onto the canvas.
            var button = new Gum.Forms.Controls.Button();
            button.Visual.WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent;
            button.Visual.HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent;
            button.Width = 0;
            button.Height = 0;
            engine.CurrentScreen.AddOverlay(button);
            var clicks = 0;
            button.Click += (_, _) => clicks++;

            // The recommended ordering: move with the button up, push, release, each on its own
            // stepped frame. Extra steps while held and after release must not add clicks.
            var commands =
                "{\"cmd\":\"input\",\"type\":\"cursor\",\"x\":32,\"y\":32}\n" +
                "{\"cmd\":\"step\"}\n" +
                "{\"cmd\":\"input\",\"type\":\"cursor\",\"x\":32,\"y\":32,\"primary\":true}\n" +
                "{\"cmd\":\"step\",\"count\":3}\n" +
                "{\"cmd\":\"input\",\"type\":\"cursor\",\"x\":32,\"y\":32}\n" +
                "{\"cmd\":\"step\",\"count\":10}\n";
            var output = new StringWriter();
            engine.StartAutomationMode(seed: 0, input: new StringReader(commands), output: output);

            // 14 step responses means every frame above has run.
            for (int i = 0; i < 200 && output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length < 14; i++)
            {
                game.RunOneFrame();
                System.Threading.Thread.Sleep(5);
            }

            clicks.ShouldBe(1);
        }
        finally
        {
            engine.Shutdown();
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
