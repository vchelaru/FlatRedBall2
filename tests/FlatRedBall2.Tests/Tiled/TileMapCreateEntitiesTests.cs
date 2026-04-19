using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Tiled;

public class TileMapCreateEntitiesTests
{
    private class MarkerEntity : Entity { }
    private class TestScreen : Screen { }

    private static Tilemap BuildTilemap(
        int widthTiles,
        int heightTiles,
        int tileSize,
        TilemapTileData[] tileDataEntries,
        (int col, int row, int localId)[] placements,
        string layerName = "Main")
    {
        var tilemap = new Tilemap(
            name: "test",
            width: widthTiles,
            height: heightTiles,
            tileWidth: tileSize,
            tileHeight: tileSize,
            orientation: TilemapOrientation.Orthogonal);

        int tileCount = 0;
        foreach (var td in tileDataEntries)
            if (td.LocalId + 1 > tileCount) tileCount = td.LocalId + 1;
        if (tileCount == 0) tileCount = 1;

        var tileset = new TilemapTileset(
            name: "ts", texture: null!, tileWidth: tileSize, tileHeight: tileSize,
            tileCount: tileCount, columns: tileCount);
        tileset.FirstGlobalId = 1;
        foreach (var td in tileDataEntries)
            tileset.AddTileData(td);
        tilemap.Tilesets.Add(tileset);

        var layer = new TilemapTileLayer(layerName, widthTiles, heightTiles, tileSize, tileSize);
        foreach (var (col, row, localId) in placements)
            layer.SetTile(col, row, new TilemapTile(globalId: 1 + localId));
        tilemap.Layers.Add(layer);

        return tilemap;
    }

    private static TilemapObjectLayer BuildObjectLayer(
        string name,
        (int id, int localId, float x, float y, int size)[] objects)
    {
        var layer = new TilemapObjectLayer(name);
        foreach (var (id, localId, x, y, size) in objects)
        {
            layer.AddObject(new TilemapTileObject(
                id: id,
                position: new XnaVec2(x, y),
                tile: new TilemapTile(globalId: 1 + localId),
                size: new XnaVec2(size, size)));
        }
        return layer;
    }

    private static (TestScreen screen, Factory<MarkerEntity> factory) NewFactory()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        return (screen, new Factory<MarkerEntity>(screen));
    }

    // ============================================================================================
    // Painted tile-layer cells
    // ============================================================================================

    [Fact]
    public void CreateEntities_PaintedCell_DefaultRemovesSourceTile()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        tileMap.CreateEntities("Coin", factory);

        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];
        liveLayer.GetTile(1, 2).HasValue.ShouldBeFalse();
    }

    [Fact]
    public void CreateEntities_PaintedCell_RemoveSourceTilesFalse_LeavesTileIntact()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        tileMap.CreateEntities("Coin", factory, removeSourceTiles: false);

        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];
        liveLayer.GetTile(1, 2)!.Value.GlobalId.ShouldBe(1);
    }

    // ============================================================================================
    // Object-layer tile-objects
    // ============================================================================================

    [Fact]
    public void CreateEntities_ObjectLayer_DefaultRemovesSourceObject()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            placements: System.Array.Empty<(int, int, int)>());
        tilemap.Layers.Add(BuildObjectLayer("Entities", new[]
        {
            (id: 1, localId: 0, x: 16f, y: 48f, size: 16),
        }));
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        tileMap.CreateEntities("Coin", factory);

        var objectLayer = (TilemapObjectLayer)tilemap.Layers[1];
        objectLayer.Objects.Count.ShouldBe(0);
    }

    [Fact]
    public void CreateEntities_ObjectLayer_RemoveSourceTilesFalse_LeavesObjectIntact()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            placements: System.Array.Empty<(int, int, int)>());
        tilemap.Layers.Add(BuildObjectLayer("Entities", new[]
        {
            (id: 1, localId: 0, x: 16f, y: 48f, size: 16),
        }));
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        tileMap.CreateEntities("Coin", factory, removeSourceTiles: false);

        var objectLayer = (TilemapObjectLayer)tilemap.Layers[1];
        objectLayer.Objects.Count.ShouldBe(1);
    }

    // ============================================================================================
    // Mixed sources — one painted + one object, default-on
    // ============================================================================================

    [Fact]
    public void CreateEntities_MixedSources_DefaultSpawnsBothAndClearsBoth()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (0, 0, 0) });
        tilemap.Layers.Add(BuildObjectLayer("Entities", new[]
        {
            (id: 1, localId: 0, x: 32f, y: 48f, size: 16),
        }));
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory);

        created.Count.ShouldBe(2);
        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];
        liveLayer.GetTile(0, 0).HasValue.ShouldBeFalse();
        var objectLayer = (TilemapObjectLayer)tilemap.Layers[1];
        objectLayer.Objects.Count.ShouldBe(0);
    }

    // ============================================================================================
    // Non-matching tiles untouched
    // ============================================================================================

    [Fact]
    public void CreateEntities_DefaultRemoval_LeavesNonMatchingTilesUntouched()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[]
            {
                new TilemapTileData(0) { Class = "Coin" },
                new TilemapTileData(1) { Class = "Solid" },
            },
            new[] { (0, 0, 0), (1, 1, 1), (2, 2, 1) });
        tilemap.Layers.Add(BuildObjectLayer("Entities", new[]
        {
            (id: 1, localId: 1, x: 48f, y: 48f, size: 16), // Solid object, should NOT be removed
        }));
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        tileMap.CreateEntities("Coin", factory);

        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];
        liveLayer.GetTile(0, 0).HasValue.ShouldBeFalse();     // Coin removed
        liveLayer.GetTile(1, 1)!.Value.GlobalId.ShouldBe(2); // Solid intact
        liveLayer.GetTile(2, 2)!.Value.GlobalId.ShouldBe(2); // Solid intact
        var objectLayer = (TilemapObjectLayer)tilemap.Layers[1];
        objectLayer.Objects.Count.ShouldBe(1);                // Solid object intact
    }

    // ============================================================================================
    // Fresh-load repeat — removal is per-load (in-memory), not persisted across reloads
    // ============================================================================================

    [Fact]
    public void CreateEntities_FreshLoadAfterRemoval_FindsTilesAgain()
    {
        // Simulate "fresh load" by constructing a second Tilemap with the same content and
        // wrapping it in a new TileMap. The first call removed source tiles from its tilemap;
        // the second, independently-built tilemap still has them.
        var first = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (0, 0, 0) });
        var firstMap = new TileMap(first);
        var (_, firstFactory) = NewFactory();
        firstMap.CreateEntities("Coin", firstFactory).Count.ShouldBe(1);

        var second = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (0, 0, 0) });
        var secondMap = new TileMap(second);
        var (_, secondFactory) = NewFactory();

        var secondCreated = secondMap.CreateEntities("Coin", secondFactory);

        secondCreated.Count.ShouldBe(1);
    }
}
