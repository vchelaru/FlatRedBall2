using RenderingLibrary;
using Shouldly;
using Xunit;
using GumRenderer = RenderingLibrary.Graphics.Renderer;

namespace FlatRedBall2.Tests.UI;

/// <summary>
/// Gum runs premultiplied by default so its textures, fonts and blend state match the engine's own
/// premultiplied sprites (issue #1526).
/// </summary>
[Collection(GraphicsDeviceCollection.Name)]
public class GumPremultipliedAlphaTests
{
    private readonly GraphicsDeviceFixture _fixture;

    public GumPremultipliedAlphaTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Initialize_UsePremultipliedGumAlpha_SetsGumRenderer(bool optOut, bool expected)
    {
        var game = _fixture.Game;
        if (SystemManagers.Default is not null || game is null)
            return;

        var settings = optOut ? new EngineInitSettings { UsePremultipliedGumAlpha = false } : new EngineInitSettings();
        GumRenderer.UsePremultipliedAlpha = !expected;

        var engine = new FlatRedBallService();
        engine.Initialize(game, settings);
        try
        {
            GumRenderer.UsePremultipliedAlpha.ShouldBe(expected);
        }
        finally
        {
            engine.Shutdown();
            GumRenderer.UsePremultipliedAlpha = false;
        }
    }
}
