using FlatRedBall2.Tests;
using FlatRedBall2.Tiled;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.GoldenImage.Tests.Scenes;

// Issue #1400. The engine suite cannot load a real TMX on CI (TiledTmxParser needs a
// GraphicsDevice), so the full path from the TMX tintcolor/opacity attributes to
// TileMapLayer.Color/Alpha is checked here, where a device exists.
[Collection(GraphicsDeviceCollection.Name)]
public class TileLayerTmxTintGoldenTests
{
    private readonly GraphicsDeviceFixture _fixture;

    public TileLayerTmxTintGoldenTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    [GoldenFact]
    public void LoadedTmx_TintColorAndOpacity_ReachLayer()
    {
        var device = GoldenDevice.Require(_fixture);

        var map = new TileMap("Tiled/Fixtures/TintedLayers.tmx", device);

        map.GetLayer("Tinted").Color.ShouldBe(new Color(0xff, 0x80, 0x40));
        map.GetLayer("Tinted").Alpha.ShouldBe(0.5f, 0.01f);
        // The tintcolor alpha channel is folded into Alpha.
        map.GetLayer("TintedWithAlpha").Color.ShouldBe(new Color(0xff, 0x00, 0x00));
        map.GetLayer("TintedWithAlpha").Alpha.ShouldBe(0x80 / 255f, 0.01f);
        map.GetLayer("Plain").Color.ShouldBe(Color.White);
        map.GetLayer("Plain").Alpha.ShouldBe(1f);
    }
}
