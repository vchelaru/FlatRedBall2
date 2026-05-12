using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Tiled;

public class TileMapCollisionsTests
{
    // Builds a minimal MonoGame.Extended Tilemap with a single tileset whose tiles have the
    // given TilemapTileData entries. The texture is null — safe because TileMapCollisions
    // only reads Class, Properties, and CollisionObjects.
    private static MonoGame.Extended.Tilemaps.Tilemap BuildTilemap(
        int widthTiles, int heightTiles, int tileSize,
        TilemapTileData[] tileDataEntries,
        (int col, int row, int localId)[] placements)
    {
        var tilemap = new MonoGame.Extended.Tilemaps.Tilemap(
            name: "test",
            width: widthTiles,
            height: heightTiles,
            tileWidth: tileSize,
            tileHeight: tileSize,
            orientation: TilemapOrientation.Orthogonal);

        int tileCount = 0;
        foreach (var td in tileDataEntries)
            if (td.LocalId + 1 > tileCount) tileCount = td.LocalId + 1;

        var tileset = new TilemapTileset(
            name: "ts",
            texture: null!,
            tileWidth: tileSize,
            tileHeight: tileSize,
            tileCount: tileCount,
            columns: tileCount);
        tileset.FirstGlobalId = 1;

        foreach (var td in tileDataEntries)
            tileset.AddTileData(td);

        tilemap.Tilesets.Add(tileset);

        var layer = new TilemapTileLayer("Main", widthTiles, heightTiles, tileSize, tileSize);
        foreach (var (col, row, localId) in placements)
            layer.SetTile(col, row, new TilemapTile(globalId: 1 + localId));
        tilemap.Layers.Add(layer);

        return tilemap;
    }

    // Variant that allows per-placement flip flags.
    private static MonoGame.Extended.Tilemaps.Tilemap BuildTilemapWithFlips(
        int widthTiles, int heightTiles, int tileSize,
        TilemapTileData[] tileDataEntries,
        (int col, int row, int localId, TilemapTileFlipFlags flip)[] placements)
    {
        var tilemap = new MonoGame.Extended.Tilemaps.Tilemap(
            name: "test", width: widthTiles, height: heightTiles,
            tileWidth: tileSize, tileHeight: tileSize,
            orientation: TilemapOrientation.Orthogonal);

        int tileCount = 0;
        foreach (var td in tileDataEntries)
            if (td.LocalId + 1 > tileCount) tileCount = td.LocalId + 1;

        var tileset = new TilemapTileset(
            name: "ts", texture: null!, tileWidth: tileSize, tileHeight: tileSize,
            tileCount: tileCount, columns: tileCount);
        tileset.FirstGlobalId = 1;
        foreach (var td in tileDataEntries) tileset.AddTileData(td);
        tilemap.Tilesets.Add(tileset);

        var layer = new TilemapTileLayer("Main", widthTiles, heightTiles, tileSize, tileSize);
        foreach (var (col, row, localId, flip) in placements)
            layer.SetTile(col, row, new TilemapTile(1 + localId, flip));
        tilemap.Layers.Add(layer);

        return tilemap;
    }

    private static TilemapTileData MakeRectTile(int localId, string className)
    {
        var td = new TilemapTileData(localId) { Class = className };
        return td;
    }

    private static TilemapTileData MakePolygonTile(int localId, string className,
        XnaVec2 position, XnaVec2[] points)
    {
        var td = new TilemapTileData(localId) { Class = className };
        td.CollisionObjects.Add(new TilemapPolygonObject(id: 1, position: position, points: points));
        return td;
    }

    // Tiled rectangle object: top-left corner (x, y), size (w, h), Y-down.
    private static TilemapTileData MakeRectObjectTile(int localId, string className,
        params (float x, float y, float w, float h)[] rects)
    {
        var td = new TilemapTileData(localId) { Class = className };
        int id = 1;
        foreach (var r in rects)
            td.CollisionObjects.Add(new TilemapRectangleObject(
                id: id++,
                position: new XnaVec2(r.x, r.y),
                size: new XnaVec2(r.w, r.h)));
        return td;
    }

    [Fact]
    public void GenerateFromClass_MatchedTileWithNoPolygon_EmitsRectOnly()
    {
        // 1x1 map, single rect-class tile at (0,0). Expect 1 rect, 0 polygons.
        var tilemap = BuildTilemap(1, 1, 16,
            tileDataEntries: [MakeRectTile(0, "Solid")],
            placements: [(0, 0, 0)]);

        var layer = (TilemapTileLayer)tilemap.Layers[0];
        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        coll.GetTileAtCell(0, 0).ShouldNotBeNull();
        coll.GetPolygonTileAtCell(0, 0).ShouldBeNull();
    }

    [Fact]
    public void GenerateFromClass_MatchedTileWithOnePolygon_EmitsPolygonOnly_WithConvertedPoints()
    {
        // Tile polygon in Tiled space (Y-down, origin top-left of tile):
        //   position (0, 0), points (0,0), (16,16), (0,16) — lower-left triangle.
        // Expected local (Y-up, centered) for G=16: (-8, 8), (8, -8), (-8, -8).
        var poly = new XnaVec2[]
        {
            new(0, 0),
            new(16, 16),
            new(0, 16),
        };
        var tilemap = BuildTilemap(1, 1, 16,
            tileDataEntries: [MakePolygonTile(0, "Solid", new XnaVec2(0, 0), poly)],
            placements: [(0, 0, 0)]);

        var layer = (TilemapTileLayer)tilemap.Layers[0];
        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        coll.GetTileAtCell(0, 0).ShouldBeNull();
        var emitted = coll.GetPolygonTileAtCell(0, 0);
        emitted.ShouldNotBeNull();
        emitted!.Points.Count.ShouldBe(3);
        emitted.Points[0].ShouldBe(new Vector2(-8f, 8f));
        emitted.Points[1].ShouldBe(new Vector2(8f, -8f));
        emitted.Points[2].ShouldBe(new Vector2(-8f, -8f));
    }

    [Fact]
    public void GenerateFromClass_MatchedTileWithTwoPolygons_Throws()
    {
        // Multi-polygon-per-cell is not supported — the second AddPolygonTileAtCell call
        // throws loudly so the authoring mistake is caught at load time instead of silently
        // dropping a collision shape.
        var pA = new XnaVec2[] { new(0, 0), new(16, 0), new(0, 16) };
        var pB = new XnaVec2[] { new(16, 16), new(16, 0), new(0, 16) };
        var td = new TilemapTileData(0) { Class = "Solid" };
        td.CollisionObjects.Add(new TilemapPolygonObject(1, new XnaVec2(0, 0), pA));
        td.CollisionObjects.Add(new TilemapPolygonObject(2, new XnaVec2(0, 0), pB));

        var tilemap = BuildTilemap(1, 1, 16,
            tileDataEntries: [td],
            placements: [(0, 0, 0)]);

        var layer = (TilemapTileLayer)tilemap.Layers[0];

        Should.Throw<System.InvalidOperationException>(
            () => TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid"));
    }

    [Fact]
    public void GenerateFromClass_MixedRectAndPolygonTiles_BothShapesEmittedAtCorrectCells()
    {
        // Map layout (Tiled Y-down rows):
        //   row 0: rect at col 0
        //   row 1: polygon at col 0
        // After Y-flip for FRB2 (height=2): Tiled row 0 -> FRB2 row 1, Tiled row 1 -> FRB2 row 0.
        var tri = new XnaVec2[] { new(0, 0), new(16, 16), new(0, 16) };
        var tilemap = BuildTilemap(1, 2, 16,
            tileDataEntries:
            [
                MakeRectTile(0, "Solid"),
                MakePolygonTile(1, "Solid", new XnaVec2(0, 0), tri),
            ],
            placements:
            [
                (0, 0, 0), // rect at Tiled (0,0) → FRB2 row 1
                (0, 1, 1), // polygon at Tiled (0,1) → FRB2 row 0
            ]);

        var layer = (TilemapTileLayer)tilemap.Layers[0];
        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        coll.GetTileAtCell(0, 1).ShouldNotBeNull();
        coll.GetPolygonTileAtCell(0, 1).ShouldBeNull();
        coll.GetTileAtCell(0, 0).ShouldBeNull();
        coll.GetPolygonTileAtCell(0, 0).ShouldNotBeNull();
    }

    // ── Flip flags ───────────────────────────────────────────────────────────
    //
    // Base polygon in Tiled pixel space: (0,0), (16,16), (0,16).
    // Centered-Y-up (no flip):           (-8,8), (8,-8), (-8,-8).
    //
    // Transforms (applied D → H → V):
    //   D: (x,y) → (-y,-x)
    //   H: (x,y) → (-x, y)
    //   V: (x,y) → ( x,-y)
    //
    // Expected outputs per flag combo (applied to the base):
    //   H only:  (8,8),  (-8,-8), (8,-8)
    //   V only:  (-8,-8),(8,8),   (-8,8)
    //   D only:  (-8,8), (8,-8),  (8,8)
    //   H+V:     (8,-8), (-8,8),  (8,8)
    //   H+V+D:   (8,-8), (-8,8),  (-8,-8)

    private static readonly XnaVec2[] BaseTriangle =
    {
        new(0, 0), new(16, 16), new(0, 16),
    };

    private static Polygon GenerateWithFlip(TilemapTileFlipFlags flip)
    {
        var tilemap = BuildTilemapWithFlips(1, 1, 16,
            tileDataEntries: [MakePolygonTile(0, "Solid", new XnaVec2(0, 0), BaseTriangle)],
            placements: [(0, 0, 0, flip)]);
        var layer = (TilemapTileLayer)tilemap.Layers[0];
        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");
        return coll.GetPolygonTileAtCell(0, 0)!;
    }

    [Fact]
    public void GenerateFromClass_PolygonTileFlippedHorizontally_PointsMirroredOnX()
    {
        var poly = GenerateWithFlip(TilemapTileFlipFlags.FlipHorizontally);
        poly.Points[0].ShouldBe(new Vector2(8f, 8f));
        poly.Points[1].ShouldBe(new Vector2(-8f, -8f));
        poly.Points[2].ShouldBe(new Vector2(8f, -8f));
    }

    [Fact]
    public void GenerateFromClass_PolygonTileFlippedVertically_PointsMirroredOnY()
    {
        var poly = GenerateWithFlip(TilemapTileFlipFlags.FlipVertically);
        poly.Points[0].ShouldBe(new Vector2(-8f, -8f));
        poly.Points[1].ShouldBe(new Vector2(8f, 8f));
        poly.Points[2].ShouldBe(new Vector2(-8f, 8f));
    }

    [Fact]
    public void GenerateFromClass_PolygonTileFlippedDiagonally_PointsReflectedAcrossDiagonal()
    {
        var poly = GenerateWithFlip(TilemapTileFlipFlags.FlipDiagonally);
        poly.Points[0].ShouldBe(new Vector2(-8f, 8f));
        poly.Points[1].ShouldBe(new Vector2(8f, -8f));
        poly.Points[2].ShouldBe(new Vector2(8f, 8f));
    }

    [Fact]
    public void GenerateFromClass_PolygonTileFlippedHorizontallyAndVertically_PointsRotated180()
    {
        var poly = GenerateWithFlip(TilemapTileFlipFlags.FlipHorizontally | TilemapTileFlipFlags.FlipVertically);
        poly.Points[0].ShouldBe(new Vector2(8f, -8f));
        poly.Points[1].ShouldBe(new Vector2(-8f, 8f));
        poly.Points[2].ShouldBe(new Vector2(8f, 8f));
    }

    [Fact]
    public void GenerateFromClass_PolygonTileAllThreeFlipFlags_PointsTransformedInDHVOrder()
    {
        var poly = GenerateWithFlip(
            TilemapTileFlipFlags.FlipHorizontally |
            TilemapTileFlipFlags.FlipVertically |
            TilemapTileFlipFlags.FlipDiagonally);
        poly.Points[0].ShouldBe(new Vector2(8f, -8f));
        poly.Points[1].ShouldBe(new Vector2(-8f, 8f));
        poly.Points[2].ShouldBe(new Vector2(-8f, -8f));
    }

    // ── Rectangle collision objects ──────────────────────────────────────────
    // Tiled <object x y width height/> with no child shape element is a rectangle.
    // Conversion (Tiled Y-down top-left → FRB2 Y-up centered, G = GridSize):
    //   center X local = x + w/2 - G/2
    //   center Y local = G/2 - (y + h/2)

    [Fact]
    public void GenerateFromClass_FullCellRectObject_EmitsSubCellRect_NoFullCellRect()
    {
        // A rect collision object covering the whole tile (0,0,16,16) still counts as
        // "author opted into custom shapes" → the default full-cell rect must NOT be added
        // via AddTileAtCell. The rect goes into the sub-cell rect list at cell center.
        var tilemap = BuildTilemap(1, 1, 16,
            tileDataEntries: [MakeRectObjectTile(0, "Solid", (0f, 0f, 16f, 16f))],
            placements: [(0, 0, 0)]);
        var layer = (TilemapTileLayer)tilemap.Layers[0];

        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        coll.GetTileAtCell(0, 0).ShouldBeNull();
        coll.GetPolygonTileAtCell(0, 0).ShouldBeNull();
        var rects = coll.GetRectangleTilesAtCell(0, 0);
        rects.Count.ShouldBe(1);
        rects[0].Width.ShouldBe(16f);
        rects[0].Height.ShouldBe(16f);
        rects[0].X.ShouldBe(8f);   // collection X=0; cell (0,0) center X = 0 + 8 = 8
        rects[0].Y.ShouldBe(-8f);  // collection Y = mapY - H*G = -16; cell (0,0) center Y = -16 + 8 = -8
    }

    [Fact]
    public void GenerateFromClass_MixedRectObjectAndPolygon_BothEmitted_NoFullCellRect()
    {
        // One tile has both a polygon and a rect — both should emit, neither suppresses the other,
        // and no default full-cell rect is added.
        var tri = new XnaVec2[] { new(0, 0), new(16, 16), new(0, 16) };
        var td = new TilemapTileData(0) { Class = "Solid" };
        td.CollisionObjects.Add(new TilemapPolygonObject(1, new XnaVec2(0, 0), tri));
        td.CollisionObjects.Add(new TilemapRectangleObject(2, new XnaVec2(0f, 8f), new XnaVec2(16f, 8f)));

        var tilemap = BuildTilemap(1, 1, 16,
            tileDataEntries: [td],
            placements: [(0, 0, 0)]);
        var layer = (TilemapTileLayer)tilemap.Layers[0];

        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        coll.GetTileAtCell(0, 0).ShouldBeNull();
        coll.GetPolygonTileAtCell(0, 0).ShouldNotBeNull();
        var rects = coll.GetRectangleTilesAtCell(0, 0);
        rects.Count.ShouldBe(1);
    }

    [Fact]
    public void GenerateFromClass_MultipleRectObjectsOnTile_BothEmittedAtCorrectCenters()
    {
        // Two rects: left half (0,0,8,16) and right half (8,0,8,16).
        // Expected local centers: (-4, 0) and (+4, 0). Cell (0,0) world center is (8, -8).
        var tilemap = BuildTilemap(1, 1, 16,
            tileDataEntries: [MakeRectObjectTile(0, "Solid",
                (0f, 0f, 8f, 16f),
                (8f, 0f, 8f, 16f))],
            placements: [(0, 0, 0)]);
        var layer = (TilemapTileLayer)tilemap.Layers[0];

        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        var rects = coll.GetRectangleTilesAtCell(0, 0);
        rects.Count.ShouldBe(2);
        // Order follows the order of collision objects on the tile.
        rects[0].X.ShouldBe(8f + -4f); // cell center + local offset
        rects[0].Y.ShouldBe(-8f + 0f);
        rects[0].Width.ShouldBe(8f);
        rects[0].Height.ShouldBe(16f);
        rects[1].X.ShouldBe(8f + 4f);
        rects[1].Y.ShouldBe(-8f + 0f);
        rects[1].Width.ShouldBe(8f);
        rects[1].Height.ShouldBe(16f);
    }

    [Fact]
    public void GenerateFromClass_RectObjectFlippedHorizontally_MirroredAcrossCellCenter()
    {
        // Left-half rect (0,0,8,16): local center (-4, 0). H-flip negates X → (+4, 0).
        var tilemap = BuildTilemapWithFlips(1, 1, 16,
            tileDataEntries: [MakeRectObjectTile(0, "Solid", (0f, 0f, 8f, 16f))],
            placements: [(0, 0, 0, TilemapTileFlipFlags.FlipHorizontally)]);
        var layer = (TilemapTileLayer)tilemap.Layers[0];

        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        var rects = coll.GetRectangleTilesAtCell(0, 0);
        rects.Count.ShouldBe(1);
        rects[0].X.ShouldBe(8f + 4f);  // right half of cell
        rects[0].Y.ShouldBe(-8f + 0f);
        rects[0].Width.ShouldBe(8f);
        rects[0].Height.ShouldBe(16f);
    }

    [Fact]
    public void GenerateFromClass_SubCellRectObject_EmitsHalfHeightRectInLowerHalf()
    {
        // Bottom half of the tile in Tiled Y-down: (0, 8, 16, 8).
        // Local center: X = 0 + 8 - 8 = 0; Y = 8 - (8 + 4) = -4. Width 16, Height 8.
        var tilemap = BuildTilemap(1, 1, 16,
            tileDataEntries: [MakeRectObjectTile(0, "Solid", (0f, 8f, 16f, 8f))],
            placements: [(0, 0, 0)]);
        var layer = (TilemapTileLayer)tilemap.Layers[0];

        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        var rects = coll.GetRectangleTilesAtCell(0, 0);
        rects.Count.ShouldBe(1);
        rects[0].Width.ShouldBe(16f);
        rects[0].Height.ShouldBe(8f);
        rects[0].X.ShouldBe(8f);          // cell center X
        rects[0].Y.ShouldBe(-8f + -4f);   // cell center Y + local -4 → lower half
    }

    [Fact]
    public void GenerateFromClass_AdjacentBottomHalfRectTiles_SuppressSharedInnerFaces()
    {
        // Author-side bottom-half rects (0,8,16,8) on the same tile; place two side-by-side.
        // After generation the two sub-cell rects form a continuous curb — their shared inner
        // faces must be suppressed via SolidSides so a mover sliding along the top
        // doesn't snag at x=16.
        var tilemap = BuildTilemap(2, 1, 16,
            tileDataEntries: [MakeRectObjectTile(0, "Solid", (0f, 8f, 16f, 8f))],
            placements: [(0, 0, 0), (1, 0, 0)]);
        var layer = (TilemapTileLayer)tilemap.Layers[0];

        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        var leftRect  = coll.GetRectangleTilesAtCell(0, 0)[0];
        var rightRect = coll.GetRectangleTilesAtCell(1, 0)[0];

        leftRect.SolidSides.ShouldBe(
            SolidSides.Up | SolidSides.Down | SolidSides.Left);
        rightRect.SolidSides.ShouldBe(
            SolidSides.Up | SolidSides.Down | SolidSides.Right);
    }

    [Fact]
    public void GenerateFromClass_SubCellRectAdjacentToPolygonTile_SuppressesRectFaceAtShared()
    {
        // Tile 0 (polygon): right-triangle slope whose right edge runs the full cell height
        // along the shared boundary. Tiled Y-down points:
        //   (0, 16), (16, 0), (16, 16) → local (Y-up, centered): (-8,-8),(8,8),(8,-8).
        // Right edge world x at cell (0,0) = 16, spans full y ∈ [0,16].
        // Tile 1 (sub-cell rect): bottom half (0, 8, 16, 8). Placed at cell (1,0) — left face at
        // x=16, y∈[0,8], fully covered by the polygon's right edge.
        // After generation, the sub-cell rect's Left bit must be cleared.
        var slopeTile = MakePolygonTile(0, "Solid", new XnaVec2(0, 0), new XnaVec2[]
        {
            new(0, 16),
            new(16, 0),
            new(16, 16),
        });
        var subCellRectTile = MakeRectObjectTile(1, "Solid", (0f, 8f, 16f, 8f));

        var tilemap = BuildTilemap(2, 1, 16,
            tileDataEntries: [slopeTile, subCellRectTile],
            placements: [(0, 0, 0), (1, 0, 1)]);
        var layer = (TilemapTileLayer)tilemap.Layers[0];

        var coll = TileMapCollisions.GenerateFromClass(tilemap, layer, "Solid");

        var rect = coll.GetRectangleTilesAtCell(1, 0)[0];
        rect.SolidSides.ShouldBe(
            SolidSides.Up | SolidSides.Down | SolidSides.Right);
    }
}
