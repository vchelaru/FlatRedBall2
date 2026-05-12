using FlatRedBall2.Collision;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Tiled;

public class TileMapReloadTests
{
    // Builds a Tilemap with one tileset and one or more tile layers. tileDataEntries defines the
    // tileset (by localId, with Class and any collision objects). placements apply only to the
    // first layer name; additional layers exist empty.
    private static Tilemap BuildTilemap(
        int widthTiles,
        int heightTiles,
        int tileSize,
        TilemapTileData[] tileDataEntries,
        (int col, int row, int localId)[] placements,
        string[]? layerNames = null)
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

        var names = layerNames ?? new[] { "Main" };
        for (int i = 0; i < names.Length; i++)
        {
            var layer = new TilemapTileLayer(names[i], widthTiles, heightTiles, tileSize, tileSize);
            if (i == 0)
                foreach (var (col, row, localId) in placements)
                    layer.SetTile(col, row, new TilemapTile(globalId: 1 + localId));
            tilemap.Layers.Add(layer);
        }

        return tilemap;
    }

    private static TilemapTileData RectTile(int localId, string className)
        => new TilemapTileData(localId) { Class = className };

    private static TilemapTileData PolygonTile(int localId, string className,
        XnaVec2 position, XnaVec2[] points)
    {
        var td = new TilemapTileData(localId) { Class = className };
        td.CollisionObjects.Add(new TilemapPolygonObject(id: 1, position: position, points: points));
        return td;
    }

    // ============================================================================================
    // Structural-change detection — should return false and leave live state untouched.
    // ============================================================================================

    [Fact]
    public void TryReload_MapWidthChanged_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(5, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_MapHeightChanged_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 5, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_TileWidthChanged_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 32, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_LayerAdded_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0) }, layerNames: new[] { "Main" });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0) }, layerNames: new[] { "Main", "Background" });

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_LayerRemoved_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0) }, layerNames: new[] { "Main", "Background" });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0) }, layerNames: new[] { "Main" });

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_LayerRenamed_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0) }, layerNames: new[] { "Main" });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0) }, layerNames: new[] { "Renamed" });

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_TilesetCountChanged_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });

        var extra = new TilemapTileset("extra", null!, 16, 16, tileCount: 1, columns: 1);
        extra.FirstGlobalId = 100;
        extra.AddTileData(new TilemapTileData(0) { Class = "Other" });
        newMap.Tilesets.Add(extra);

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_ObjectLayerAdded_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });

        newMap.Layers.Add(new TilemapObjectLayer("Entities"));

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_ObjectLayerObjectAdded_ReturnsFalse()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });

        oldMap.Layers.Add(new TilemapObjectLayer("Entities"));

        var newObjects = new TilemapObjectLayer("Entities");
        newObjects.AddObject(new TilemapTileObject(
            id: 1,
            position: new XnaVec2(16, 16),
            tile: new TilemapTile(1),
            size: new XnaVec2(16, 16)));
        newMap.Layers.Add(newObjects);

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeFalse();
    }

    [Fact]
    public void TryReload_FailedReload_LeavesLiveStateUnchanged()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(5, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (1, 1, 0) });

        var tileMap = new TileMap(oldMap);
        var beforeWidth = tileMap.Width;
        var beforeHeight = tileMap.Height;

        tileMap.TryReload(newMap).ShouldBeFalse();

        tileMap.Width.ShouldBe(beforeWidth);
        tileMap.Height.ShouldBe(beforeHeight);
        var liveLayer = (TilemapTileLayer)oldMap.Layers[0];
        liveLayer.GetTile(0, 0)!.Value.GlobalId.ShouldBe(1);
        liveLayer.GetTile(1, 1).HasValue.ShouldBeFalse();
    }

    // ============================================================================================
    // In-place tile-data updates — tile id changes, additions, removals.
    // The TSC must reflect the new tile data; the live TilemapTileLayer must hold the new tile.
    // ============================================================================================

    // TileShapes cells use Y-up: TMX (col, row) maps to TSC (col, height-1-row).
    // With a height-4 map: TMX (0,0) -> TSC (0,3); TMX (1,1) -> TSC (1,2); TMX (2,2) -> TSC (2,1).
    private const int H = 4;
    private static (int col, int row) Tsc(int col, int tmxRow) => (col, H - 1 - tmxRow);

    [Fact]
    public void TryReload_NoChanges_ReturnsTrue_TscUnchanged()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });

        var tileMap = new TileMap(oldMap);
        var solid = tileMap.GenerateCollisionFromClass("Solid");

        tileMap.TryReload(newMap).ShouldBeTrue();

        var (c, r) = Tsc(0, 0);
        solid.GetTileAtCell(c, r).ShouldNotBeNull();
    }

    [Fact]
    public void TryReload_TileAddedToEmptyCell_TscGainsCell()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0), (1, 1, 0) });

        var tileMap = new TileMap(oldMap);
        var solid = tileMap.GenerateCollisionFromClass("Solid");
        var (c, r) = Tsc(1, 1);
        // Sanity — TSC starts without that cell.
        solid.GetTileAtCell(c, r).ShouldBeNull();

        tileMap.TryReload(newMap).ShouldBeTrue();

        solid.GetTileAtCell(c, r).ShouldNotBeNull();
    }

    [Fact]
    public void TryReload_TileRemovedFromCell_TscLosesCell()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0), (1, 1, 0) });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0) });

        var tileMap = new TileMap(oldMap);
        var solid = tileMap.GenerateCollisionFromClass("Solid");
        var (c, r) = Tsc(1, 1);
        solid.GetTileAtCell(c, r).ShouldNotBeNull();

        tileMap.TryReload(newMap).ShouldBeTrue();

        solid.GetTileAtCell(c, r).ShouldBeNull();
    }

    [Fact]
    public void TryReload_TileGidChangedToDifferentClass_TscLosesCell()
    {
        var oldMap = BuildTilemap(4, 4, 16,
            new[] { RectTile(0, "Solid"), RectTile(1, "Other") },
            new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 16,
            new[] { RectTile(0, "Solid"), RectTile(1, "Other") },
            new[] { (0, 0, 1) }); // localId 1 = "Other"

        var tileMap = new TileMap(oldMap);
        var solid = tileMap.GenerateCollisionFromClass("Solid");
        var (c, r) = Tsc(0, 0);
        solid.GetTileAtCell(c, r).ShouldNotBeNull();

        tileMap.TryReload(newMap).ShouldBeTrue();

        solid.GetTileAtCell(c, r).ShouldBeNull();
    }

    [Fact]
    public void TryReload_LiveLayerReflectsNewTileData()
    {
        var oldMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") }, new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 16, new[] { RectTile(0, "Solid") },
            new[] { (0, 0, 0), (2, 2, 0) });

        var tileMap = new TileMap(oldMap);

        tileMap.TryReload(newMap).ShouldBeTrue();

        // The live TilemapTileLayer (held by the renderer) must reflect the new placement.
        var liveLayer = (TilemapTileLayer)oldMap.Layers[0];
        liveLayer.GetTile(2, 2)!.Value.GlobalId.ShouldBe(1);
    }

    // ============================================================================================
    // Polygon and rect tile-shape changes — when a cell's tile id changes from a plain-rect tile
    // to a polygon-collision tile (or reverse), the TSC must contain the new shape kind.
    // ============================================================================================

    [Fact]
    public void TryReload_CellChangedFromRectToPolygon_TscHasPolygon()
    {
        var triangle = new[]
        {
            new XnaVec2(0, 0), new XnaVec2(16, 16), new XnaVec2(0, 16),
        };
        var oldMap = BuildTilemap(4, 4, 16,
            new[]
            {
                RectTile(0, "Solid"),
                PolygonTile(1, "Solid", new XnaVec2(0, 0), triangle),
            },
            new[] { (0, 0, 0) });
        var newMap = BuildTilemap(4, 4, 16,
            new[]
            {
                RectTile(0, "Solid"),
                PolygonTile(1, "Solid", new XnaVec2(0, 0), triangle),
            },
            new[] { (0, 0, 1) });

        var tileMap = new TileMap(oldMap);
        var solid = tileMap.GenerateCollisionFromClass("Solid");
        var (c, r) = Tsc(0, 0);
        solid.GetTileAtCell(c, r).ShouldNotBeNull();         // started as rect
        solid.GetPolygonTileAtCell(c, r).ShouldBeNull();

        tileMap.TryReload(newMap).ShouldBeTrue();

        solid.GetTileAtCell(c, r).ShouldBeNull();            // no longer a rect
        solid.GetPolygonTileAtCell(c, r).ShouldNotBeNull();  // now a polygon
    }

    [Fact]
    public void TryReload_CellChangedFromPolygonToRect_TscHasRectOnly()
    {
        var triangle = new[]
        {
            new XnaVec2(0, 0), new XnaVec2(16, 16), new XnaVec2(0, 16),
        };
        var oldMap = BuildTilemap(4, 4, 16,
            new[]
            {
                RectTile(0, "Solid"),
                PolygonTile(1, "Solid", new XnaVec2(0, 0), triangle),
            },
            new[] { (0, 0, 1) });
        var newMap = BuildTilemap(4, 4, 16,
            new[]
            {
                RectTile(0, "Solid"),
                PolygonTile(1, "Solid", new XnaVec2(0, 0), triangle),
            },
            new[] { (0, 0, 0) });

        var tileMap = new TileMap(oldMap);
        var solid = tileMap.GenerateCollisionFromClass("Solid");
        var (c, r) = Tsc(0, 0);
        solid.GetPolygonTileAtCell(c, r).ShouldNotBeNull();
        solid.GetTileAtCell(c, r).ShouldBeNull();

        tileMap.TryReload(newMap).ShouldBeTrue();

        solid.GetPolygonTileAtCell(c, r).ShouldBeNull();
        solid.GetTileAtCell(c, r).ShouldNotBeNull();
    }

    // ============================================================================================
    // Multiple tracked TSCs — both registered collections must update on a single reload.
    // ============================================================================================

    [Fact]
    public void TryReload_TwoTrackedCollections_BothUpdate()
    {
        var oldMap = BuildTilemap(4, 4, 16,
            new[]
            {
                RectTile(0, "Solid"),
                RectTile(1, "JumpThrough"),
            },
            new[] { (0, 0, 0), (1, 1, 1) });
        var newMap = BuildTilemap(4, 4, 16,
            new[]
            {
                RectTile(0, "Solid"),
                RectTile(1, "JumpThrough"),
            },
            new[] { (0, 0, 0), (2, 2, 1) }); // jump-through moves from (1,1) to (2,2)

        var tileMap = new TileMap(oldMap);
        var solid = tileMap.GenerateCollisionFromClass("Solid");
        var jumpThrough = tileMap.GenerateCollisionFromClass("JumpThrough");

        var s00 = Tsc(0, 0);
        var j11 = Tsc(1, 1);
        var j22 = Tsc(2, 2);

        solid.GetTileAtCell(s00.col, s00.row).ShouldNotBeNull();
        jumpThrough.GetTileAtCell(j11.col, j11.row).ShouldNotBeNull();
        jumpThrough.GetTileAtCell(j22.col, j22.row).ShouldBeNull();

        tileMap.TryReload(newMap).ShouldBeTrue();

        solid.GetTileAtCell(s00.col, s00.row).ShouldNotBeNull();          // unchanged
        jumpThrough.GetTileAtCell(j11.col, j11.row).ShouldBeNull();       // gone
        jumpThrough.GetTileAtCell(j22.col, j22.row).ShouldNotBeNull();    // moved here
    }
}
