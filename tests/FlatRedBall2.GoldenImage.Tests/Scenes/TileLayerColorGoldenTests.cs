using FlatRedBall2.Tests;
using FlatRedBall2.Tiled;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;

namespace FlatRedBall2.GoldenImage.Tests.Scenes;

// Issue #1400. TileMapLayer.Color / Alpha applied at draw time. Each scene is four 16px columns.
// Every assertion on a pixel value is independent of the golden, so a wrong golden cannot enshrine
// wrong output.
//
// The marker tile (global id 4) is a white 5x5 square at its top-left over solid red (200, 40, 50),
// so a sample inside the square shows the tint color directly and one outside shows tint * red.
[Collection(GraphicsDeviceCollection.Name)]
public class TileLayerColorGoldenTests
{
    private const int Columns = 4;
    private const int Rows = 1;
    private const int Size = SceneArt.TileSize;

    private static readonly Color Background = new(24, 32, 64);
    private static readonly Color Red = new(200, 40, 50);
    // Dirt tile (global id 2) pixel at local (2, 2) and (10, 10): (x + y) % 6 != 0 in both.
    private static readonly Color Dirt = new(150, 105, 60);

    private readonly GraphicsDeviceFixture _fixture;

    public TileLayerColorGoldenTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    private static Tilemap NewTilemap(GraphicsDevice device)
    {
        var tilemap = new Tilemap("test", Columns, Rows, Size, Size, TilemapOrientation.Orthogonal);
        tilemap.Tilesets.Add(new TilemapTileset("strip", SceneArt.TileStrip(device), Size, Size, tileCount: 4, columns: 4)
        {
            FirstGlobalId = 1,
        });
        return tilemap;
    }

    private static TilemapTileLayer AddMarkerLayer(Tilemap tilemap, string name, int col)
    {
        var layer = new TilemapTileLayer(name, Columns, Rows, Size, Size);
        layer.SetTile(col, 0, new TilemapTile(globalId: 4));
        tilemap.Layers.Add(layer);
        return layer;
    }

    private static void AddDirtGround(Tilemap tilemap)
    {
        var ground = new TilemapTileLayer("Ground", Columns, Rows, Size, Size);
        for (int col = 0; col < Columns; col++)
            ground.SetTile(col, 0, new TilemapTile(globalId: 2));
        tilemap.Layers.Add(ground);
    }

    private static PixelImage Render(GraphicsDevice device, TileMap map)
    {
        var screen = new Screen { Engine = new FlatRedBallService() };
        screen.Add(map);
        // The map's top-left is the world origin and it extends down, so center the camera on it.
        return OffscreenRenderer.Render(device, screen, Columns * Size, Rows * Size,
            cameraX: Columns * Size / 2f, cameraY: -Rows * Size / 2f, background: Background);
    }

    // Sample inside a column's white square (2, 2) or on its red body (10, 10).
    private static Color White(PixelImage image, int col) => image[col * Size + 2, 2];
    private static Color Body(PixelImage image, int col) => image[col * Size + 10, 10];

    private static void ShouldBeNear(Color actual, Color expected, int tolerance = 3)
    {
        System.Math.Abs(actual.R - expected.R).ShouldBeLessThanOrEqualTo(tolerance, $"R of {actual} vs {expected}");
        System.Math.Abs(actual.G - expected.G).ShouldBeLessThanOrEqualTo(tolerance, $"G of {actual} vs {expected}");
        System.Math.Abs(actual.B - expected.B).ShouldBeLessThanOrEqualTo(tolerance, $"B of {actual} vs {expected}");
    }

    private static Color Mix(Color top, Color below, float topWeight) => new(
        (int)System.MathF.Round(top.R * topWeight + below.R * (1 - topWeight)),
        (int)System.MathF.Round(top.G * topWeight + below.G * (1 - topWeight)),
        (int)System.MathF.Round(top.B * topWeight + below.B * (1 - topWeight)));

    [GoldenFact]
    public void Draw_TintedLayerNextToUntintedLayers_TintsOnlyThatLayer()
    {
        var device = GoldenDevice.Require(_fixture);
        var tilemap = NewTilemap(device);
        AddMarkerLayer(tilemap, "Untinted", col: 0);
        AddMarkerLayer(tilemap, "Orange", col: 1);
        AddMarkerLayer(tilemap, "Cyan", col: 2);
        var map = new TileMap(tilemap, device);

        map.GetLayer("Orange").Color = new Color(255, 128, 64);
        map.GetLayer("Cyan").Color = new Color(60, 200, 255);
        var image = Render(device, map);

        // Untinted layer: white square and red body untouched.
        ShouldBeNear(White(image, 0), Color.White);
        ShouldBeNear(Body(image, 0), Red);
        // White * tint is the tint; red * tint is the per-channel product.
        ShouldBeNear(White(image, 1), new Color(255, 128, 64));
        ShouldBeNear(Body(image, 1), new Color(200 * 255 / 255, 40 * 128 / 255, 50 * 64 / 255));
        ShouldBeNear(White(image, 2), new Color(60, 200, 255));
        ShouldBeNear(Body(image, 2), new Color(200 * 60 / 255, 40 * 200 / 255, 50 * 255 / 255));
        // The empty fourth column is just the background.
        ShouldBeNear(White(image, 3), Background);

        GoldenImage.Verify("tile-layer-tint", image);
    }

    [GoldenFact]
    public void Draw_FadedOverlayLayers_BlendWithTheGroundBelow()
    {
        var device = GoldenDevice.Require(_fixture);
        var tilemap = NewTilemap(device);
        AddDirtGround(tilemap);
        AddMarkerLayer(tilemap, "Opaque", col: 0);
        AddMarkerLayer(tilemap, "Half", col: 1);
        AddMarkerLayer(tilemap, "Invisible", col: 2);
        AddMarkerLayer(tilemap, "OrangeHalf", col: 3);
        var map = new TileMap(tilemap, device);

        map.GetLayer("Half").Alpha = 0.5f;
        map.GetLayer("Invisible").Alpha = 0f;
        map.GetLayer("OrangeHalf").Color = new Color(255, 128, 64);
        map.GetLayer("OrangeHalf").Alpha = 0.5f;
        var image = Render(device, map);

        ShouldBeNear(White(image, 0), Color.White);
        ShouldBeNear(White(image, 1), Mix(Color.White, Dirt, 0.5f));
        ShouldBeNear(Body(image, 1), Mix(Red, Dirt, 0.5f));
        // Alpha 0 draws nothing: the ground shows through unchanged.
        ShouldBeNear(White(image, 2), Dirt);
        ShouldBeNear(Body(image, 2), Dirt);
        // Tint and alpha compose: tinted marker at half weight over the ground.
        ShouldBeNear(White(image, 3), Mix(new Color(255, 128, 64), Dirt, 0.5f));
        ShouldBeNear(Body(image, 3), Mix(new Color(200, 20, 13), Dirt, 0.5f));

        GoldenImage.Verify("tile-layer-alpha", image);
    }

    [GoldenFact]
    public void Draw_LayerColorChangedAfterAFirstDraw_IsPickedUpOnTheNextDraw()
    {
        var device = GoldenDevice.Require(_fixture);
        var tilemap = NewTilemap(device);
        AddMarkerLayer(tilemap, "Main", col: 1);
        var map = new TileMap(tilemap, device);

        ShouldBeNear(White(Render(device, map), 1), Color.White);

        // Fading in steps (the Cranky Chibi Cthulhu boss-room case) means re-setting every frame.
        foreach (var layer in map.Layers)
        {
            layer.Color = Color.Red;
            layer.Alpha = 0.5f;
        }
        var image = Render(device, map);

        ShouldBeNear(White(image, 1), Mix(Color.Red, Background, 0.5f));
    }
}
