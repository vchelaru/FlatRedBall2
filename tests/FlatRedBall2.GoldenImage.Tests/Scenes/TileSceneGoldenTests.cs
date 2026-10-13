using System;
using FlatRedBall2.Tests;
using FlatRedBall2.Tiled;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Tilemaps;
using Xunit;

namespace FlatRedBall2.GoldenImage.Tests.Scenes;

[Collection(GraphicsDeviceCollection.Name)]
public class TileSceneGoldenTests
{
    private const int Columns = 6;
    private const int Rows = 4;
    private const int Size = SceneArt.TileSize;

    private readonly GraphicsDeviceFixture _fixture;

    public TileSceneGoldenTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    // Global ids are local id + 1 (FirstGlobalId = 1). TileStrip: 0 grass, 1 dirt, 2 water, 3 marker.
    private static Tilemap NewTilemap(GraphicsDevice device, Action<TilemapTileset>? configureTileset = null)
    {
        var tilemap = new Tilemap("test", Columns, Rows, Size, Size, TilemapOrientation.Orthogonal);
        var tileset = new TilemapTileset("strip", SceneArt.TileStrip(device), Size, Size, tileCount: 4, columns: 4)
        {
            FirstGlobalId = 1,
        };
        configureTileset?.Invoke(tileset);
        tilemap.Tilesets.Add(tileset);
        return tilemap;
    }

    private static Screen NewScreen() => new() { Engine = new FlatRedBallService() };

    private static FrameTime Frame(float seconds) =>
        new(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(seconds), TimeSpan.Zero, TimeSpan.Zero);

    private static PixelImage Render(GraphicsDevice device, Screen screen) =>
        // The map's top-left is the world origin and it extends down, so center the camera on it.
        OffscreenRenderer.Render(device, screen, Columns * Size, Rows * Size,
            cameraX: Columns * Size / 2f, cameraY: -Rows * Size / 2f);

    [GoldenFact]
    public void Draw_TwoTileLayersWithAFlippedTile_MatchesGolden()
    {
        var device = GoldenDevice.Require(_fixture);
        var tilemap = NewTilemap(device);

        var ground = new TilemapTileLayer("Ground", Columns, Rows, Size, Size);
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
                ground.SetTile(col, row, new TilemapTile(globalId: row >= 2 ? 2 : col >= 4 ? 3 : 1)); // grass, water top-right, dirt below
        tilemap.Layers.Add(ground);

        var overlay = new TilemapTileLayer("Overlay", Columns, Rows, Size, Size);
        overlay.SetTile(1, 1, new TilemapTile(globalId: 4));
        overlay.SetTile(3, 1, new TilemapTile(4, TilemapTileFlipFlags.FlipHorizontally)); // marker square moves to the top-right
        overlay.SetTile(5, 3, new TilemapTile(4, TilemapTileFlipFlags.FlipVertically));   // ... bottom-left
        tilemap.Layers.Add(overlay);

        var screen = NewScreen();
        screen.Add(new TileMap(tilemap, device));

        GoldenImage.Verify("tile-layers", Render(device, screen));
    }

    // Local tile 0 alternates grass -> water, 0.5s each. Row 0 is animated; row 1 is static dirt
    // and water so a whole-layer redraw problem would show up next to the animated cells.
    private static Screen AnimatedScene(GraphicsDevice device)
    {
        var animation = new TilemapTileAnimation(new[]
        {
            new TilemapTileAnimationFrame(0, 0.5f),
            new TilemapTileAnimationFrame(2, 0.5f),
        });
        var tilemap = NewTilemap(device, tileset => tileset.AddTileData(new TilemapTileData(0) { Animation = animation }));

        var layer = new TilemapTileLayer("Main", Columns, Rows, Size, Size);
        for (int col = 0; col < Columns; col++)
        {
            layer.SetTile(col, 0, new TilemapTile(globalId: 1)); // animated
            layer.SetTile(col, 1, new TilemapTile(globalId: col % 2 == 0 ? 2 : 3)); // static dirt and water
        }
        tilemap.Layers.Add(layer);

        var screen = NewScreen();
        screen.Add(new TileMap(tilemap, device));
        return screen;
    }

    [GoldenFact]
    public void Draw_AnimatedTilesBeforeAnyUpdate_ShowFirstFrame()
    {
        var device = GoldenDevice.Require(_fixture);
        var screen = AnimatedScene(device);

        GoldenImage.Verify("tile-animation-frame0", Render(device, screen));
    }

    [GoldenFact]
    public void Draw_AnimatedTilesAfterScreenUpdate_ShowSecondFrame()
    {
        var device = GoldenDevice.Require(_fixture);
        var screen = AnimatedScene(device);

        screen.Update(Frame(0.6f)); // past the first frame's 0.5s

        GoldenImage.Verify("tile-animation-frame1", Render(device, screen));
    }
}
