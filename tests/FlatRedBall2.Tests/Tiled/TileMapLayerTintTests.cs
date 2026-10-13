using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using FlatRedBall2.Tiled;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Tiled.Converters;
using MonoGame.Extended.Tilemaps.Tiled.Document;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

// Issue #1400. Runtime Color/Alpha on TileMapLayer wrap MonoGame.Extended's TilemapLayer
// TintColor/Opacity, which its renderer applies at draw time. These run headless (CI has no GPU);
// the real-TMX fixture uses an image-less tileset.
public class TileMapLayerTintTests
{
    // Relative: TitleContainer resolves against the test output directory and rejects rooted paths.
    private const string FixturePath = "Tiled/Fixtures/TintedLayers.tmx";

    private static (TileMap map, TilemapTileLayer live) BuildHandMade()
    {
        var tilemap = new Tilemap("t", 2, 2, 16, 16, TilemapOrientation.Orthogonal);
        var live = new TilemapTileLayer("Main", 2, 2, 16, 16);
        tilemap.Layers.Add(live);
        return (new TileMap(tilemap), live);
    }

    [Fact]
    public void Defaults_AreWhiteAndFullyOpaque()
    {
        var (map, _) = BuildHandMade();
        var layer = map.GetLayer("Main");

        layer.Color.ShouldBe(Color.White);
        layer.Alpha.ShouldBe(1f);
    }

    [Fact]
    public void Color_Set_ReachesUnderlyingLayerTintColor()
    {
        var (map, live) = BuildHandMade();

        map.GetLayer("Main").Color = new Color(255, 128, 64);

        live.TintColor.ShouldBe(new Color(255, 128, 64, 255));
        map.GetLayer("Main").Color.ShouldBe(new Color(255, 128, 64));
    }

    [Fact]
    public void Alpha_Set_ReachesUnderlyingLayerOpacity()
    {
        var (map, live) = BuildHandMade();

        map.GetLayer("Main").Alpha = 0.25f;

        map.GetLayer("Main").Alpha.ShouldBe(0.25f);
        // The fade rides in the tint's alpha with Opacity left at 1: MGEx's renderer multiplies
        // RGB by Opacity too, which a non-premultiplied blend then applies a second time.
        live.TintColor!.Value.A.ShouldBe((byte)64);
        live.Opacity.ShouldBe(1f);
    }

    [Fact]
    public void Color_Set_KeepsAlphaAndAlpha_Set_KeepsColor()
    {
        var (map, live) = BuildHandMade();
        var layer = map.GetLayer("Main");

        layer.Alpha = 0.5f;
        layer.Color = new Color(10, 20, 30, 200); // the color's own alpha channel is ignored

        layer.Alpha.ShouldBe(0.5f);
        layer.Color.ShouldBe(new Color(10, 20, 30));
        live.TintColor.ShouldBe(new Color(10, 20, 30, 128));
    }

    [Fact]
    public void Construction_LayerWithOpacityAndTint_FoldsOpacityIntoAlpha()
    {
        var tilemap = new Tilemap("t", 2, 2, 16, 16, TilemapOrientation.Orthogonal);
        var live = new TilemapTileLayer("Main", 2, 2, 16, 16)
        {
            Opacity = 0.5f,
            TintColor = new Color(255, 0, 0, 128), // what a TMX tintcolor="#80ff0000" parses to
        };
        tilemap.Layers.Add(live);

        var layer = new TileMap(tilemap).GetLayer("Main");

        layer.Color.ShouldBe(new Color(255, 0, 0));
        layer.Alpha.ShouldBe(0.5f * 128 / 255f, 0.001f);
        live.Opacity.ShouldBe(1f);
    }

    [Fact]
    public void Setters_AffectOnlyThatLayer()
    {
        var tilemap = new Tilemap("t", 2, 2, 16, 16, TilemapOrientation.Orthogonal);
        var a = new TilemapTileLayer("A", 2, 2, 16, 16);
        var b = new TilemapTileLayer("B", 2, 2, 16, 16);
        tilemap.Layers.Add(a);
        tilemap.Layers.Add(b);
        var map = new TileMap(tilemap);

        map.GetLayer("A").Alpha = 0f;
        map.GetLayer("A").Color = Color.Red;

        b.Opacity.ShouldBe(1f);
        b.TintColor.ShouldBeNull();
    }

    // TiledTmxParser.ParseFromStream rejects a null GraphicsDevice, so a TMX cannot be loaded into
    // a Tilemap on CI. This runs the parser's document-to-data steps (the part that reads
    // tintcolor/opacity) without a device; the golden project's TileLayerTmxTintGoldenTests cover the full TileMap path.
    private static TilemapData ConvertFixture()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, FixturePath));
        var xml = (TiledMapXml)new XmlSerializer(typeof(TiledMapXml)).Deserialize(stream)!;
        return TiledTilemapDataConverter.Convert(xml);
    }

    private static TilemapLayerData DataLayer(TilemapData data, string name) =>
        data.Layers.Single(l => l.Name == name);

    [Fact]
    public void MgexConversion_TintColorAndOpacity_AreReadFromTmx()
    {
        var tinted = DataLayer(ConvertFixture(), "Tinted");

        tinted.TintColor.ShouldBe(new Color(0xff, 0x80, 0x40));
        tinted.Opacity.ShouldBe(0.5f);
    }

    [Fact]
    public void MgexConversion_TintColorWithAlphaChannel_IsParsedAsArgb()
    {
        // Tiled writes #AARRGGBB.
        DataLayer(ConvertFixture(), "TintedWithAlpha").TintColor
            .ShouldBe(new Color(0xff, 0x00, 0x00, 0x80));
    }

    [Fact]
    public void MgexConversion_LayerWithoutTintAttributes_HasNoTintAndFullOpacity()
    {
        var plain = DataLayer(ConvertFixture(), "Plain");

        plain.TintColor.ShouldBeNull();
        plain.Opacity.ShouldBe(1f);
    }
}
