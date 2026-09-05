using System;
using Gum.GueDeriving;
using KernSmith;
using Microsoft.Xna.Framework;
using RenderingLibrary;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.UI;

/// <summary>
/// Covers issue #1000: Solitaire's text blurs under zoom/resize because Gum bakes bitmap font
/// atlases at a fixed design size. Gum's own <see cref="TextRuntime.UseFontOversampling"/> rebuilds
/// the font at the zoomed size instead of stretching the baked bitmap, but only if a game turns the
/// flag on — <see cref="FlatRedBallService"/> must do this once, since it already wires the
/// <c>IInMemoryFontCreator</c> that oversampling requires unconditionally.
/// </summary>
[Collection(GraphicsDeviceCollection.Name)]
public class FontOversamplingTests
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
            System.Diagnostics.Debug.WriteLine($"[tests] No graphics device available: {e.Message}");
            return null;
        }
    }

    [Fact]
    public void Initialize_TurnsOnFontOversampling()
    {
        if (GumIsOwnedElsewhere)
            return;

        using var game = TryCreateGame();
        if (game is null)
            return;

        var engine = new FlatRedBallService();
        engine.Initialize(game);

        TextRuntime.UseFontOversampling.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, RasterizerBackend.StbTrueType)]
    public void ResolveFontRasterizerBackend_SelectsStbTrueTypeOnlyOnBrowser(bool isBrowser, RasterizerBackend? expected)
    {
        FlatRedBallService.ResolveFontRasterizerBackend(isBrowser).ShouldBe(expected);
    }
}
