using System;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using RenderingLibrary;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.UI;

/// <summary>
/// Covers the KernSmith in-memory font generator that <see cref="FlatRedBallService.Initialize(Game, EngineInitSettings?)"/>
/// wires up as Gum's <c>IInMemoryFontCreator</c>. Requires a real <see cref="GraphicsDevice"/> because
/// KernSmith rasterizes fonts through it — see issue #855, where a version mismatch between
/// <c>Gum.MonoGame</c> and <c>KernSmith.MonoGameGum</c> in <c>Directory.Packages.props</c> made in-memory
/// font generation throw internally (caught and silently swallowed by Gum), leaving every
/// <see cref="TextRuntime"/> sharing whatever font happened to be cached first regardless of its own
/// <see cref="TextRuntime.FontSize"/>.
/// </summary>
[Collection(GraphicsDeviceCollection.Name)]
public class DynamicFontGenerationTests
{
    private static bool GumIsOwnedElsewhere => SystemManagers.Default is not null;

    private static Game? TryCreateGame()
    {
        try
        {
            var game = new Game();
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
    public void FontSize_TwoDistinctSizes_ProduceDistinctlyRasterizedFonts()
    {
        if (GumIsOwnedElsewhere)
            return;

        // Deliberately not disposed: disposing this ad-hoc Game tears down process-wide GL/SDL
        // state that the shared GraphicsDeviceFixture's device depends on, breaking shader
        // compilation for every GraphicsDeviceFixture-based test that runs afterward in this
        // process (see SpriteAddColorRenderTests, discovered while adding it). The process exits
        // once the test run finishes, so leaking this one Game for the run's lifetime is fine.
        var game = TryCreateGame();
        if (game is null)
            return;

        var engine = new FlatRedBallService();
        engine.Initialize(game);

        var small = new TextRuntime { Font = "Arial", FontSize = 20 };
        var large = new TextRuntime { Font = "Arial", FontSize = 90 };

        small.Typeface.ShouldNotBeNull();
        large.Typeface.ShouldNotBeNull();
        large.Typeface.LineHeightInPixels.ShouldBeGreaterThan(small.Typeface.LineHeightInPixels);
    }
}
