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
    // Origin — every value maps to the correct corner/edge of the tile object's rect.
    // Object at Tiled position (16, 48), size 16x16 → world bottom-left (16, -48).
    // ============================================================================================

    [Theory]
    [InlineData(Origin.Center, 24f, -40f)]
    [InlineData(Origin.BottomCenter, 24f, -48f)]
    [InlineData(Origin.TopCenter, 24f, -32f)]
    [InlineData(Origin.BottomLeft, 16f, -48f)]
    [InlineData(Origin.TopLeft, 16f, -32f)]
    [InlineData(Origin.BottomRight, 32f, -48f)]
    [InlineData(Origin.TopRight, 32f, -32f)]
    public void CreateEntities_ObjectLayer_OriginPlacesEntityAtExpectedCorner(Origin origin, float expectedX, float expectedY)
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

        var created = tileMap.CreateEntities("Coin", factory, origin);

        created[0].X.ShouldBe(expectedX);
        created[0].Y.ShouldBe(expectedY);
    }

    [Theory]
    [InlineData(Origin.Center, 24f, -40f)]
    [InlineData(Origin.BottomLeft, 16f, -48f)]
    [InlineData(Origin.TopRight, 32f, -32f)]
    public void CreateEntities_PaintedCell_OriginPlacesEntityAtExpectedCorner(Origin origin, float expectedX, float expectedY)
    {
        // Tile (1, 2) on a 4x4 16-px map: bottom-left at (16, -48) — same rect as the object-layer case above.
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, origin);

        created[0].X.ShouldBe(expectedX);
        created[0].Y.ShouldBe(expectedY);
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

    // ============================================================================================
    // Lazy spawn — factory opts in via LazySpawn != Disabled
    // ============================================================================================

    [Fact]
    public void CreateEntities_LazyMode_DoesNotSpawnImmediately()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();
        factory.LazySpawn = LazySpawnMode.OneShot;

        var created = tileMap.CreateEntities("Coin", factory);

        created.Count.ShouldBe(0);
        factory.Count.ShouldBe(0);
    }

    [Fact]
    public void CreateEntities_LazyMode_StillRemovesSourceTilesAtLoad()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();
        factory.LazySpawn = LazySpawnMode.OneShot;

        tileMap.CreateEntities("Coin", factory);

        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];
        liveLayer.GetTile(1, 2).HasValue.ShouldBeFalse();
    }

    [Fact]
    public void CreateEntities_LazyMode_ManagerSpawnsAtRecordedPosition()
    {
        // Tile (1, 2) on a 4x4 16-px map: bottom-left at (16, _y - 48); center at (24, _y - 40).
        // _y defaults to 0 → world position (24, -40).
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();
        factory.LazySpawn = LazySpawnMode.OneShot;

        tileMap.CreateEntities("Coin", factory);
        // Activation rect that overlaps (24, -40)
        tileMap.LazySpawner.Update(left: 0f, right: 100f, bottom: -100f, top: 100f);

        factory.Count.ShouldBe(1);
        factory[0].X.ShouldBe(24f);
        factory[0].Y.ShouldBe(-40f);
    }

    [Fact]
    public void CreateEntities_LazyMode_CustomPropertiesAppliedAtSpawnTime()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            placements: System.Array.Empty<(int, int, int)>());

        var objLayer = new TilemapObjectLayer("Entities");
        var tileObj = new TilemapTileObject(
            id: 1,
            position: new XnaVec2(16f, 48f),
            tile: new TilemapTile(globalId: 1),
            size: new XnaVec2(16, 16));
        tileObj.Properties.SetString("Tag", "ruby");
        objLayer.AddObject(tileObj);
        tilemap.Layers.Add(objLayer);

        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<TaggedEntity>(screen);
        factory.LazySpawn = LazySpawnMode.OneShot;

        tileMap.CreateEntities("Coin", factory);

        // Before spawn: nothing exists yet, so reflection cannot have run.
        factory.Count.ShouldBe(0);

        // Activate the rect to spawn.
        tileMap.LazySpawner.Update(-100f, 100f, -100f, 100f);

        factory.Count.ShouldBe(1);
        factory[0].Tag.ShouldBe("ruby");
    }

    private class TaggedEntity : Entity
    {
        public string? Tag { get; set; }
    }

    [Fact]
    public void CreateEntities_ConfigureCallback_RunsAfterTiledPropertiesInEagerMode()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        var tileObj = new TilemapTileObject(
            id: 1,
            position: new XnaVec2(16f, 48f),
            tile: new TilemapTile(globalId: 1),
            size: new XnaVec2(16, 16));
        tileObj.Properties.SetString("Tag", "from-tiled");
        objLayer.AddObject(tileObj);
        tilemap.Layers.Add(objLayer);

        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<TaggedEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory,
            configure: e => e.Tag = e.Tag + "+configured");

        created.Count.ShouldBe(1);
        created[0].Tag.ShouldBe("from-tiled+configured");
    }

    [Fact]
    public void CreateEntities_ConfigureCallback_RunsAtSpawnTimeInLazyMode()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();
        factory.LazySpawn = LazySpawnMode.OneShot;

        int configureCalls = 0;
        tileMap.CreateEntities("Coin", factory, configure: _ => configureCalls++);

        configureCalls.ShouldBe(0);

        tileMap.LazySpawner.Update(-100f, 100f, -100f, 100f);

        configureCalls.ShouldBe(1);
    }

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

    // ============================================================================================
    // Class-level (tileset type) properties, instance-level overrides, and TiledGid
    // ============================================================================================

    private class PropertyEntity : Entity
    {
        public int Worth { get; set; }
        public string? Label { get; set; }
        public int TiledGid { get; set; }
    }

    [Fact]
    public void CreateEntities_PaintedCell_ClassLevelPropertyApplies()
    {
        var tileData = new TilemapTileData(0) { Class = "Coin" };
        tileData.Properties.SetInt("Worth", 50);
        var tilemap = BuildTilemap(4, 4, 16, new[] { tileData }, new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].Worth.ShouldBe(50);
    }

    [Fact]
    public void CreateEntities_ObjectLayer_ClassLevelPropertyAppliesWithNoInstanceOverride()
    {
        var tileData = new TilemapTileData(0) { Class = "Coin" };
        tileData.Properties.SetInt("Worth", 50);
        var tilemap = BuildTilemap(4, 4, 16, new[] { tileData },
            placements: System.Array.Empty<(int, int, int)>());
        tilemap.Layers.Add(BuildObjectLayer("Entities", new[]
        {
            (id: 1, localId: 0, x: 16f, y: 48f, size: 16),
        }));
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].Worth.ShouldBe(50);
    }

    [Fact]
    public void CreateEntities_ObjectLayer_InstancePropertyOverridesClassLevelProperty()
    {
        var tileData = new TilemapTileData(0) { Class = "Coin" };
        tileData.Properties.SetInt("Worth", 50);
        var tilemap = BuildTilemap(4, 4, 16, new[] { tileData },
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        var tileObj = new TilemapTileObject(
            id: 1,
            position: new XnaVec2(16f, 48f),
            tile: new TilemapTile(globalId: 1),
            size: new XnaVec2(16, 16));
        tileObj.Properties.SetInt("Worth", 99);
        objLayer.AddObject(tileObj);
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].Worth.ShouldBe(99);
    }

    [Fact]
    public void CreateEntities_PaintedCell_TiledGidMatchesSpawningTileGid()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].TiledGid.ShouldBe(1); // FirstGlobalId (1) + localId (0)
    }

    [Fact]
    public void CreateEntities_ObjectLayer_TiledGidMatchesSpawningTileGid()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(1) { Class = "Coin" } },
            placements: System.Array.Empty<(int, int, int)>());
        tilemap.Layers.Add(BuildObjectLayer("Entities", new[]
        {
            (id: 1, localId: 1, x: 16f, y: 48f, size: 16),
        }));
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].TiledGid.ShouldBe(2); // FirstGlobalId (1) + localId (1)
    }

    [Fact]
    public void CreateEntities_EntityWithoutTiledGidProperty_SpawnsWithoutException()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory(); // MarkerEntity declares no TiledGid property

        var created = tileMap.CreateEntities("Coin", factory);

        created.Count.ShouldBe(1);
    }

    [Fact]
    public void CreateEntities_LazyMode_ClassLevelPropertyAndTiledGidAppliedAtSpawnTime()
    {
        var tileData = new TilemapTileData(0) { Class = "Coin" };
        tileData.Properties.SetInt("Worth", 50);
        var tilemap = BuildTilemap(4, 4, 16, new[] { tileData }, new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);
        factory.LazySpawn = LazySpawnMode.OneShot;

        tileMap.CreateEntities("Coin", factory);
        tileMap.LazySpawner.Update(-100f, 100f, -100f, 100f);

        factory.Count.ShouldBe(1);
        factory[0].Worth.ShouldBe(50);
        factory[0].TiledGid.ShouldBe(1); // FirstGlobalId (1) + localId (0)
    }

    private class UIntGidEntity : Entity
    {
        public uint TiledGid { get; set; }
    }

    private class LongGidEntity : Entity
    {
        public long TiledGid { get; set; }
    }

    private class ULongGidEntity : Entity
    {
        public ulong TiledGid { get; set; }
    }

    [Fact]
    public void CreateEntities_PaintedCell_TiledGidPopulatesUIntProperty()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<UIntGidEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].TiledGid.ShouldBe(1u); // FirstGlobalId (1) + localId (0)
    }

    [Fact]
    public void CreateEntities_PaintedCell_TiledGidPopulatesLongProperty()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<LongGidEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].TiledGid.ShouldBe(1L); // FirstGlobalId (1) + localId (0)
    }

    [Fact]
    public void CreateEntities_PaintedCell_TiledGidPopulatesULongProperty()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { new TilemapTileData(0) { Class = "Coin" } },
            new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<ULongGidEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].TiledGid.ShouldBe(1ul); // FirstGlobalId (1) + localId (0)
    }

    // ============================================================================================
    // Non-tile object shapes — point / rectangle markers spawn too
    // ============================================================================================

    [Theory]
    [InlineData(Origin.Center)]
    [InlineData(Origin.BottomLeft)]
    [InlineData(Origin.TopRight)]
    public void CreateEntities_PointObject_SpawnsAtPointRegardlessOfOrigin(Origin origin)
    {
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapPointObject(id: 1, position: new XnaVec2(32f, 48f))
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, origin);

        created.Count.ShouldBe(1);
        created[0].X.ShouldBe(32f);
        created[0].Y.ShouldBe(-48f);
    }

    [Fact]
    public void CreateEntities_PointObject_DefaultRemovesSourceObject()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapPointObject(id: 1, position: new XnaVec2(32f, 48f))
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        tileMap.CreateEntities("Coin", factory);

        var objectLayer = (TilemapObjectLayer)tilemap.Layers[1];
        objectLayer.Objects.Count.ShouldBe(0);
    }

    [Fact]
    public void CreateEntities_PointObject_InstancePropertyAppliesAndTiledGidIsZero()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        var pointObj = new TilemapPointObject(id: 1, position: new XnaVec2(32f, 48f))
        {
            Class = "Coin",
        };
        pointObj.Properties.SetInt("Worth", 99);
        objLayer.AddObject(pointObj);
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].Worth.ShouldBe(99);
        created[0].TiledGid.ShouldBe(0); // point objects carry no gid
    }

    [Fact]
    public void CreateEntities_InstancePropertyKeyCasingDifference_AppliesCaseInsensitively()
    {
        // Tiled authors freely mix property casing (e.g. "pos" vs the C# "Pos"); matching
        // must be case-insensitive or the value silently never reaches the entity.
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        var pointObj = new TilemapPointObject(id: 1, position: new XnaVec2(32f, 48f))
        {
            Class = "Coin",
        };
        pointObj.Properties.SetString("label", "gold");
        objLayer.AddObject(pointObj);
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].Label.ShouldBe("gold");
    }

    [Fact]
    public void CreateEntities_NonMatchingPointObject_IsSkippedAndLeftIntact()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapPointObject(id: 1, position: new XnaVec2(32f, 48f))
        {
            Class = "Solid",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory);

        created.Count.ShouldBe(0); // "Solid" point doesn't match "Coin"
        ((TilemapObjectLayer)tilemap.Layers[1]).Objects.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(Origin.Center, 24f, -56f)]
    [InlineData(Origin.BottomLeft, 16f, -64f)]
    [InlineData(Origin.TopRight, 32f, -48f)]
    public void CreateEntities_RectangleObject_OriginPlacesEntityAtExpectedCorner(Origin origin, float expectedX, float expectedY)
    {
        // Rectangle objects anchor at their top-left corner in Tiled: pos (16, 48), size 16
        // → world top-left (16, -48), center (24, -56).
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapRectangleObject(
            id: 1,
            position: new XnaVec2(16f, 48f),
            size: new XnaVec2(16f, 16f))
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, origin);

        created[0].X.ShouldBe(expectedX);
        created[0].Y.ShouldBe(expectedY);
    }

    [Fact]
    public void CreateEntities_LazyMode_PointObjectSpawnsAtRecordedPosition()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapPointObject(id: 1, position: new XnaVec2(32f, 48f))
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();
        factory.LazySpawn = LazySpawnMode.OneShot;

        tileMap.CreateEntities("Coin", factory);
        tileMap.LazySpawner.Update(left: 0f, right: 100f, bottom: -100f, top: 100f);

        factory.Count.ShouldBe(1);
        factory[0].X.ShouldBe(32f);
        factory[0].Y.ShouldBe(-48f);
    }

    // ============================================================================================
    // Rotated object markers
    // ============================================================================================

    [Fact]
    public void CreateEntities_RectangleObjectRotated180Degrees_SpawnsAtRotatedCenter()
    {
        // Same geometry as TileMapCollisionsTests'
        // GenerateFromClass_ObjectLayerRect180DegreesRotated_PivotsAroundPositionNotCenter, so the
        // two paths can be compared directly. Tiled rotates clockwise around the object's own
        // (x,y), NOT its bounding-box center: position (16,0) size (16,8) rotated 180 deg lands on
        // the opposite side of the pivot, occupying world X:[0,16] Y:[0,8] — center (8,4). Ignoring
        // Rotation puts the entity at (24,-4), which is outside the marker entirely.
        var tilemap = BuildTilemap(2, 2, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapRectangleObject(
            id: 1,
            position: new XnaVec2(16f, 0f),
            size: new XnaVec2(16f, 8f))
        {
            Class = "Coin",
            Rotation = System.MathF.PI,
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created.Count.ShouldBe(1);
        created[0].X.ShouldBe(8f);
        created[0].Y.ShouldBe(4f);
    }

    [Fact]
    public void CreateEntities_TileObjectRotated90Degrees_SpawnsAtRotatedCenter()
    {
        // Tile objects anchor at their bottom-left, so the tile sits ABOVE its own (x,y) in
        // Tiled's Y-down space: position (16,32) size 16 occupies Tiled X:[16,32] Y:[16,32],
        // center offset (8,-8) from the pivot. Rotating 90 deg clockwise about the pivot maps
        // that offset to (8,8), i.e. Tiled center (24,40) -> world (24,-40). Unrotated the same
        // marker centers at world (24,-24), so this fails if Rotation is dropped.
        var tileData = new TilemapTileData(0) { Class = "Coin" };
        var tilemap = BuildTilemap(4, 4, 16, new[] { tileData },
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapTileObject(
            id: 1,
            position: new XnaVec2(16f, 32f),
            tile: new TilemapTile(globalId: 1),
            size: new XnaVec2(16f, 16f))
        {
            Rotation = System.MathF.PI / 2f,
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created.Count.ShouldBe(1);
        created[0].X.ShouldBe(24f);
        created[0].Y.ShouldBe(-40f);
    }

    [Fact]
    public void CreateEntities_EllipseObjectRotated180Degrees_SpawnsAtRotatedCenter()
    {
        // Ellipses anchor at their bounding rect's top-left, same as rectangles: position (16,0)
        // size (16,8) puts the center 8 right and 4 down of the pivot. Rotated 180 deg that
        // offset flips to (-8,-4) -> Tiled center (8,-4) -> world (8,4).
        var tilemap = BuildTilemap(2, 2, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapEllipseObject(
            id: 1,
            position: new XnaVec2(16f, 0f),
            size: new XnaVec2(16f, 8f))
        {
            Class = "Coin",
            Rotation = System.MathF.PI,
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created.Count.ShouldBe(1);
        created[0].X.ShouldBe(8f);
        created[0].Y.ShouldBe(4f);
    }

    [Fact]
    public void CreateEntities_PointObjectRotated_IgnoresRotation()
    {
        // A point has no extent, so there is nothing for rotation to swing around its own pivot.
        // Guards the shared rotation path from moving zero-size markers.
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapPointObject(id: 1, position: new XnaVec2(32f, 48f))
        {
            Class = "Coin",
            Rotation = System.MathF.PI / 4f,
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created[0].X.ShouldBe(32f);
        created[0].Y.ShouldBe(-48f);
    }

    // ============================================================================================
    // Object shapes with no usable extent — polygon, polyline, text
    // ============================================================================================

    [Fact]
    public void CreateEntities_PolygonObject_SpawnsAtObjectPosition()
    {
        // A polygon's Position is its origin vertex, not a centroid or a bounding-box corner —
        // the marker point is the position itself, so Origin has nothing to offset against.
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapPolygonObject(
            id: 1,
            position: new XnaVec2(16f, 48f),
            points: new[] { new XnaVec2(0f, 0f), new XnaVec2(16f, 16f), new XnaVec2(0f, 16f) })
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created.Count.ShouldBe(1);
        created[0].X.ShouldBe(16f);
        created[0].Y.ShouldBe(-48f);
    }

    [Fact]
    public void CreateEntities_PolylineObject_SpawnsAtObjectPosition()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapPolylineObject(
            id: 1,
            position: new XnaVec2(48f, 16f),
            points: new[] { new XnaVec2(0f, 0f), new XnaVec2(32f, 0f) })
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created.Count.ShouldBe(1);
        created[0].X.ShouldBe(48f);
        created[0].Y.ShouldBe(-16f);
    }

    [Fact]
    public void CreateEntities_TextObject_IgnoresBoundingSizeAndSpawnsAtPosition()
    {
        // TilemapTextObject carries a Size (the text's wrap box), but that box is a typesetting
        // hint, not a marker body — treating it as extent would put Origin.Center in the middle of
        // the text block instead of on the authored point. Expected (16,-32), not (48,-40).
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapTextObject(
            id: 1,
            position: new XnaVec2(16f, 32f),
            size: new XnaVec2(64f, 16f),
            text: "spawn here")
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created.Count.ShouldBe(1);
        created[0].X.ShouldBe(16f);
        created[0].Y.ShouldBe(-32f);
    }

    // ============================================================================================
    // Map offset folded into non-tile object placement
    // ============================================================================================

    [Fact]
    public void CreateEntities_RectangleObjectOnOffsetMap_FoldsMapPositionIntoSpawnPoint()
    {
        // Map top-left at (100,200); rect at Tiled (16,48) size 16 -> world top-left (116,152),
        // center (124,144). Proves the map's own offset reaches the rectangle path.
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapRectangleObject(
            id: 1, position: new XnaVec2(16f, 48f), size: new XnaVec2(16f, 16f))
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap, x: 100f, y: 200f);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created[0].X.ShouldBe(124f);
        created[0].Y.ShouldBe(144f);
    }

    [Fact]
    public void CreateEntities_EllipseObjectOnOffsetMap_FoldsMapPositionIntoSpawnPoint()
    {
        // Map top-left at (100,200); ellipse bounding rect at Tiled (16,48) size (16,8) -> center
        // offset (8,4) from the anchor -> world (124,148).
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapEllipseObject(
            id: 1, position: new XnaVec2(16f, 48f), size: new XnaVec2(16f, 8f))
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap, x: 100f, y: 200f);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Coin", factory, Origin.Center);

        created[0].X.ShouldBe(124f);
        created[0].Y.ShouldBe(148f);
    }

    [Fact]
    public void CreateEntities_RectangleObject_DefaultRemovesSourceObject()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapRectangleObject(
            id: 1, position: new XnaVec2(16f, 48f), size: new XnaVec2(16f, 16f))
        {
            Class = "Coin",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        tileMap.CreateEntities("Coin", factory);

        ((TilemapObjectLayer)tilemap.Layers[1]).Objects.Count.ShouldBe(0);
    }

    // ============================================================================================
    // Property key casing across every property source
    // ============================================================================================

    [Fact]
    public void CreateEntities_PaintedCell_ClassPropertyKeyCasingDiffers_StillApplies()
    {
        var tileData = new TilemapTileData(0) { Class = "Coin" };
        tileData.Properties.SetInt("worth", 50);
        var tilemap = BuildTilemap(4, 4, 16, new[] { tileData }, new[] { (1, 2, 0) });
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].Worth.ShouldBe(50);
    }

    [Fact]
    public void CreateEntities_TileObject_ClassPropertyKeyCasingDiffers_StillApplies()
    {
        var tileData = new TilemapTileData(0) { Class = "Coin" };
        tileData.Properties.SetInt("worth", 50);
        var tilemap = BuildTilemap(4, 4, 16, new[] { tileData },
            placements: System.Array.Empty<(int, int, int)>());
        tilemap.Layers.Add(BuildObjectLayer("Entities", new[]
        {
            (id: 1, localId: 0, x: 16f, y: 48f, size: 16),
        }));
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].Worth.ShouldBe(50);
    }

    [Fact]
    public void CreateEntities_InstanceAndClassPropertyKeysDifferOnlyByCase_InstanceStillWins()
    {
        // The tileset declares "Worth" and the placed instance overrides "worth". Merged with an
        // ordinal comparer both keys survive and the class-level one wins the lookup, so the
        // instance override silently does nothing. Instance must win regardless of casing.
        var tileData = new TilemapTileData(0) { Class = "Coin" };
        tileData.Properties.SetInt("Worth", 50);
        var tilemap = BuildTilemap(4, 4, 16, new[] { tileData },
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        var tileObj = new TilemapTileObject(
            id: 1,
            position: new XnaVec2(16f, 48f),
            tile: new TilemapTile(globalId: 1),
            size: new XnaVec2(16, 16));
        tileObj.Properties.SetInt("worth", 99);
        objLayer.AddObject(tileObj);
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<PropertyEntity>(screen);

        var created = tileMap.CreateEntities("Coin", factory);

        created[0].Worth.ShouldBe(99);
    }

    // ============================================================================================
    // Consume-once contract with GenerateCollisionFromClass
    // ============================================================================================

    [Fact]
    public void CreateEntities_RectangleObjectThenGenerateCollision_ObjectIsAlreadyConsumed()
    {
        // CreateEntities removes what it matches, so a class feeding both systems must run
        // CreateEntities first — the later collision pass finds nothing left to build from.
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapRectangleObject(
            id: 1, position: new XnaVec2(0f, 0f), size: new XnaVec2(16f, 16f))
        {
            Class = "Door",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var created = tileMap.CreateEntities("Door", factory);
        var collision = tileMap.GenerateCollisionFromClass("Door");

        created.Count.ShouldBe(1);
        collision.AllTiles.ShouldBeEmpty();
    }

    [Fact]
    public void CreateEntities_GenerateCollisionFirst_ShapeAndEntityBothExist()
    {
        // The mirror of the above: collision generation consumes nothing, so running it first
        // leaves the object in place for CreateEntities and one marker yields two runtime objects.
        var tilemap = BuildTilemap(4, 4, 16,
            tileDataEntries: System.Array.Empty<TilemapTileData>(),
            placements: System.Array.Empty<(int, int, int)>());
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapRectangleObject(
            id: 1, position: new XnaVec2(0f, 0f), size: new XnaVec2(16f, 16f))
        {
            Class = "Door",
        });
        tilemap.Layers.Add(objLayer);
        var tileMap = new TileMap(tilemap);
        var (_, factory) = NewFactory();

        var collision = tileMap.GenerateCollisionFromClass("Door");
        var created = tileMap.CreateEntities("Door", factory);

        collision.AllTiles.ShouldHaveSingleItem();
        created.Count.ShouldBe(1);
    }
}
