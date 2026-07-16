using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Tiled;

public class TileMapGetObjectLayerDataTests
{
    private static Tilemap BuildObjectOnlyTilemap(
        int widthTiles, int heightTiles, int tileSize, TilemapObjectLayer objectLayer, TilemapTileset? tileset = null)
    {
        var tilemap = new Tilemap(
            name: "test",
            width: widthTiles,
            height: heightTiles,
            tileWidth: tileSize,
            tileHeight: tileSize,
            orientation: TilemapOrientation.Orthogonal);
        if (tileset != null)
            tilemap.Tilesets.Add(tileset);
        tilemap.Layers.Add(objectLayer);
        return tilemap;
    }

    [Fact]
    public void GetObjectLayerData_LayerNameCaseInsensitive_FindsLayer()
    {
        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(new TilemapRectangleObject(id: 1, position: new XnaVec2(0, 0), size: new XnaVec2(16, 16)));
        var tileMap = new TileMap(BuildObjectOnlyTilemap(2, 2, 16, layer));

        var entries = tileMap.GetObjectLayerData("WATER");

        entries.Count.ShouldBe(1);
    }

    [Fact]
    public void GetObjectLayerData_MixedObjectTypes_ReturnsOneEntryPerObjectInAuthoringOrder()
    {
        var tileset = new TilemapTileset(name: "ts", texture: null!, tileWidth: 16, tileHeight: 16, tileCount: 1, columns: 1);
        tileset.FirstGlobalId = 1;
        tileset.AddTileData(new TilemapTileData(0));

        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(new TilemapRectangleObject(id: 1, position: new XnaVec2(0, 0), size: new XnaVec2(16, 16)) { Class = "Rect" });
        layer.AddObject(new TilemapPointObject(id: 2, position: new XnaVec2(0, 0)) { Class = "Point" });
        layer.AddObject(new TilemapPolygonObject(
            id: 3, position: new XnaVec2(0, 0), points: new[] { new XnaVec2(0, 0), new XnaVec2(1, 1), new XnaVec2(0, 1) }) { Class = "Poly" });
        layer.AddObject(new TilemapTileObject(
            id: 4, position: new XnaVec2(0, 16), tile: new TilemapTile(globalId: 1), size: new XnaVec2(16, 16)) { Class = "TileObj" });
        var tileMap = new TileMap(BuildObjectOnlyTilemap(2, 2, 16, layer, tileset));

        var entries = tileMap.GetObjectLayerData("Water");

        entries.Count.ShouldBe(4);
        entries[0].Class.ShouldBe("Rect");
        entries[1].Class.ShouldBe("Point");
        entries[2].Class.ShouldBe("Poly");
        entries[3].Class.ShouldBe("TileObj");
    }

    [Fact]
    public void GetObjectLayerData_NonExistentLayer_ReturnsEmptyList()
    {
        var layer = new TilemapObjectLayer("Water");
        var tileMap = new TileMap(BuildObjectOnlyTilemap(2, 2, 16, layer));

        var entries = tileMap.GetObjectLayerData("DoesNotExist");

        entries.ShouldBeEmpty();
    }

    [Fact]
    public void GetObjectLayerData_NoTilemapLoaded_ReturnsEmptyList()
    {
        var tileMap = new TileMap(width: 64f, height: 64f, tileWidth: 16, tileHeight: 16, layers: new List<TileMapLayer>());

        var entries = tileMap.GetObjectLayerData("Water");

        entries.ShouldBeEmpty();
    }

    [Fact]
    public void GetObjectLayerData_PlainObject_ReturnsAnchorPositionWithZeroSize()
    {
        var pointObj = new TilemapPointObject(id: 1, position: new XnaVec2(24, 8)) { Class = "Spawn" };
        pointObj.Properties.SetString("Kind", "Player");
        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(pointObj);
        var tileMap = new TileMap(BuildObjectOnlyTilemap(2, 2, 16, layer), x: 50f, y: 60f);

        var entries = tileMap.GetObjectLayerData("Water");

        entries.Count.ShouldBe(1);
        entries[0].X.ShouldBe(74f);
        entries[0].Y.ShouldBe(52f);
        entries[0].Width.ShouldBe(0f);
        entries[0].Height.ShouldBe(0f);
        entries[0].GlobalId.ShouldBe(0);
        entries[0].Class.ShouldBe("Spawn");
        entries[0].Points.ShouldBeNull();
        entries[0].Properties["Kind"].ShouldBe("Player");
    }

    [Fact]
    public void GetObjectLayerData_PolygonObject_NullPointsReturnsNullPointsEntry()
    {
        // TilemapPolygonObject.Points has a public setter and TileMapCollisions already guards
        // against it being null (AddPolygonObject) — a reachable state, not a hypothetical one.
        var polyObj = new TilemapPolygonObject(
            id: 1, position: new XnaVec2(0, 0),
            points: new[] { new XnaVec2(0, 0), new XnaVec2(16, 16), new XnaVec2(0, 16) });
        polyObj.Points = null!;
        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(polyObj);
        var tileMap = new TileMap(BuildObjectOnlyTilemap(2, 2, 16, layer));

        var entries = tileMap.GetObjectLayerData("Water");

        entries.Count.ShouldBe(1);
        entries[0].Points.ShouldBeNull();
    }

    [Fact]
    public void GetObjectLayerData_PolygonObject_ReturnsWorldSpacePoints()
    {
        // Local triangle (0,0),(16,16),(0,16) at Tiled position (16,40), map at (200,300) — both
        // the object's own anchor position and the map offset are nonzero so this also proves the
        // entry's own X/Y reflect the world-space anchor (not just Points), and that the map
        // offset is folded into polygon conversion the same as it is for rects/tile-objects.
        // World point = (mapX + Position.X + localX, mapY - (Position.Y + localY)) — no
        // rotation applied, consistent with this method ignoring Rotation for every object type.
        var polyObj = new TilemapPolygonObject(
            id: 1, position: new XnaVec2(16, 40),
            points: new[] { new XnaVec2(0, 0), new XnaVec2(16, 16), new XnaVec2(0, 16) }) { Class = "Zone" };
        polyObj.Properties.SetString("Terrain", "reef");
        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(polyObj);
        var tileMap = new TileMap(BuildObjectOnlyTilemap(2, 2, 16, layer), x: 200f, y: 300f);

        var entries = tileMap.GetObjectLayerData("Water");

        entries.Count.ShouldBe(1);
        entries[0].X.ShouldBe(216f);
        entries[0].Y.ShouldBe(260f);
        entries[0].Points.ShouldNotBeNull();
        entries[0].Points!.Count.ShouldBe(3);
        entries[0].Points![0].ShouldBe(new Vector2(216f, 260f));
        entries[0].Points![1].ShouldBe(new Vector2(232f, 244f));
        entries[0].Points![2].ShouldBe(new Vector2(216f, 244f));
        entries[0].Width.ShouldBe(0f);
        entries[0].Height.ShouldBe(0f);
        entries[0].GlobalId.ShouldBe(0);
        entries[0].Class.ShouldBe("Zone");
        entries[0].Properties["Terrain"].ShouldBe("reef");
    }

    [Fact]
    public void GetObjectLayerData_RectangleObject_PointsIsNull()
    {
        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(new TilemapRectangleObject(id: 1, position: new XnaVec2(0, 0), size: new XnaVec2(16, 16)));
        var tileMap = new TileMap(BuildObjectOnlyTilemap(2, 2, 16, layer));

        var entries = tileMap.GetObjectLayerData("Water");

        entries[0].Points.ShouldBeNull();
    }

    [Fact]
    public void GetObjectLayerData_RectangleObject_ReturnsWorldSpaceTopLeftAndSize()
    {
        // TileMap positioned away from world origin to prove the map offset is folded in —
        // raw Tiled-space coords would be wrong here.
        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(new TilemapRectangleObject(
            id: 1, position: new XnaVec2(10, 20), size: new XnaVec2(30, 40)) { Class = "Zone" });
        var tileMap = new TileMap(BuildObjectOnlyTilemap(4, 4, 16, layer), x: 100f, y: 200f);

        var entries = tileMap.GetObjectLayerData("Water");

        entries.Count.ShouldBe(1);
        entries[0].X.ShouldBe(110f);
        entries[0].Y.ShouldBe(180f);
        entries[0].Width.ShouldBe(30f);
        entries[0].Height.ShouldBe(40f);
        entries[0].Class.ShouldBe("Zone");
        entries[0].GlobalId.ShouldBe(0);
    }

    [Fact]
    public void GetObjectLayerData_RectangleObject_UsesInstancePropertiesOnly()
    {
        var rectObj = new TilemapRectangleObject(id: 1, position: new XnaVec2(0, 0), size: new XnaVec2(16, 16));
        rectObj.Properties.SetString("Zone", "Water");
        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(rectObj);
        var tileMap = new TileMap(BuildObjectOnlyTilemap(2, 2, 16, layer));

        var entries = tileMap.GetObjectLayerData("Water");

        entries[0].Properties["Zone"].ShouldBe("Water");
    }

    [Fact]
    public void GetObjectLayerData_TileObject_ConvertsBottomLeftAnchorToTopLeftAndSetsGlobalId()
    {
        // Tile object at Tiled position (16, 48), size 16x16 — same fixture geometry as
        // TileMapCreateEntitiesTests' Origin.TopLeft case, which expects world (16, -32).
        var tileData = new TilemapTileData(0) { Class = "Coast" };
        var tileset = new TilemapTileset(name: "ts", texture: null!, tileWidth: 16, tileHeight: 16, tileCount: 1, columns: 1);
        tileset.FirstGlobalId = 1;
        tileset.AddTileData(tileData);

        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(new TilemapTileObject(
            id: 1, position: new XnaVec2(16, 48), tile: new TilemapTile(globalId: 1), size: new XnaVec2(16, 16)));
        var tileMap = new TileMap(BuildObjectOnlyTilemap(4, 4, 16, layer, tileset));

        var entries = tileMap.GetObjectLayerData("Water");

        entries[0].X.ShouldBe(16f);
        entries[0].Y.ShouldBe(-32f);
        entries[0].Width.ShouldBe(16f);
        entries[0].Height.ShouldBe(16f);
        entries[0].GlobalId.ShouldBe(1);
        entries[0].Points.ShouldBeNull();
    }

    [Fact]
    public void GetObjectLayerData_TileObject_FoldsInMapOffset()
    {
        // Same object geometry as the ConvertsBottomLeftAnchor test, but with the map itself
        // moved off the origin — proves the map offset combines correctly with the
        // bottom-left-to-top-left flip, not just with the simpler top-left-anchored formula
        // rectangle/plain/polygon objects use.
        var tileset = new TilemapTileset(name: "ts", texture: null!, tileWidth: 16, tileHeight: 16, tileCount: 1, columns: 1);
        tileset.FirstGlobalId = 1;
        tileset.AddTileData(new TilemapTileData(0));

        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(new TilemapTileObject(
            id: 1, position: new XnaVec2(16, 48), tile: new TilemapTile(globalId: 1), size: new XnaVec2(16, 16)));
        var tileMap = new TileMap(BuildObjectOnlyTilemap(4, 4, 16, layer, tileset), x: 100f, y: 50f);

        var entries = tileMap.GetObjectLayerData("Water");

        entries[0].X.ShouldBe(116f);
        entries[0].Y.ShouldBe(18f);
    }

    [Fact]
    public void GetObjectLayerData_TileObject_MergesClassAndInstanceProperties()
    {
        var tileData = new TilemapTileData(0) { Class = "Coast" };
        tileData.Properties.SetString("Terrain", "grass");
        tileData.Properties.SetString("Side", "top");
        var tileset = new TilemapTileset(name: "ts", texture: null!, tileWidth: 16, tileHeight: 16, tileCount: 1, columns: 1);
        tileset.FirstGlobalId = 1;
        tileset.AddTileData(tileData);

        var tileObj = new TilemapTileObject(
            id: 1, position: new XnaVec2(0, 16), tile: new TilemapTile(globalId: 1), size: new XnaVec2(16, 16));
        tileObj.Properties.SetString("Terrain", "sand"); // instance overrides class
        var layer = new TilemapObjectLayer("Water");
        layer.AddObject(tileObj);
        var tileMap = new TileMap(BuildObjectOnlyTilemap(4, 4, 16, layer, tileset));

        var entries = tileMap.GetObjectLayerData("Water");

        entries[0].Properties["Terrain"].ShouldBe("sand");
        entries[0].Properties["Side"].ShouldBe("top");
    }
}
