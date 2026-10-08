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
    private readonly GraphicsDeviceFixture _fixture;

    public DynamicFontGenerationTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    private static bool GumIsOwnedElsewhere => SystemManagers.Default is not null;

    [Fact]
    public void FontSize_TwoDistinctSizes_ProduceDistinctlyRasterizedFonts()
    {
        if (GumIsOwnedElsewhere)
            return;

        var game = _fixture.Game;
        if (game is null)
            return;

        var engine = new FlatRedBallService();
        engine.Initialize(game);
        try
        {
            var small = new TextRuntime { Font = "Arial", FontSize = 20 };
            var large = new TextRuntime { Font = "Arial", FontSize = 90 };

            small.Typeface.ShouldNotBeNull();
            large.Typeface.ShouldNotBeNull();
            large.Typeface.LineHeightInPixels.ShouldBeGreaterThan(small.Typeface.LineHeightInPixels);
        }
        finally
        {
            engine.Shutdown();
        }
    }
}
