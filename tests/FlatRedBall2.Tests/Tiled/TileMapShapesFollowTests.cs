using System.Linq;
using FlatRedBall2.Collision;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Tiled;

// TileShapes produced by GenerateCollisionFrom* must follow the TileMap when it moves, the way
// the map's tile layers do.
public class TileMapShapesFollowTests
{
    private const int Size = 4;
    private const int Tile = 16;

    // 4x4 map. Tileset tile 0 = plain rect "Solid"; tile 1 = triangle polygon "Slope" that also
    // carries the property "Hazard". placements are (col, tmxRow, localId).
    private static Tilemap BuildTilemap((int col, int row, int localId)[] placements)
    {
        var tilemap = new Tilemap("test", Size, Size, Tile, Tile, TilemapOrientation.Orthogonal);
        var tileset = new TilemapTileset("ts", null!, Tile, Tile, tileCount: 2, columns: 2)
        {
            FirstGlobalId = 1,
        };
        var solid = new TilemapTileData(0) { Class = "Solid" };
        var slope = new TilemapTileData(1) { Class = "Slope" };
        slope.CollisionObjects.Add(new TilemapPolygonObject(id: 1, position: XnaVec2.Zero, points: new[]
        {
            new XnaVec2(0, 0), new XnaVec2(16, 16), new XnaVec2(0, 16),
        }));
        tileset.AddTileData(solid);
        tileset.AddTileData(slope);
        tilemap.Tilesets.Add(tileset);

        var layer = new TilemapTileLayer("Main", Size, Size, Tile, Tile);
        foreach (var (col, row, localId) in placements)
            layer.SetTile(col, row, new TilemapTile(globalId: 1 + localId));
        tilemap.Layers.Add(layer);

        return tilemap;
    }

    private static TileMap BuildMap((int col, int row, int localId)[] placements) =>
        new TileMap(BuildTilemap(placements));

    // TMX row 3 (bottom row) is TileShapes row 0.
    private static (int col, int row) Cell(int col, int tmxRow) => (col, Size - 1 - tmxRow);

    [Fact]
    public void MapXY_Changed_RectTilesFollow()
    {
        var map = BuildMap(new[] { (0, 3, 0) });
        var solid = map.GenerateCollisionFromClass("Solid");
        var (c, r) = Cell(0, 3);
        var tile = solid.GetTileAtCell(c, r)!;
        float x0 = tile.X, y0 = tile.Y;

        map.X += 100f;
        map.Y -= 30f;

        tile.X.ShouldBe(x0 + 100f);
        tile.Y.ShouldBe(y0 - 30f);
    }

    [Fact]
    public void MapXY_Changed_PolygonTilesFollow()
    {
        var map = BuildMap(new[] { (1, 2, 1) });
        var slopes = map.GenerateCollisionFromClass("Slope");
        var (c, r) = Cell(1, 2);
        var poly = slopes.GetPolygonTileAtCell(c, r)!;
        float x0 = poly.X, y0 = poly.Y;

        map.X = 64f;
        map.Y = 48f;

        poly.X.ShouldBe(x0 + 64f);
        poly.Y.ShouldBe(y0 + 48f);
    }

    [Fact]
    public void CenterOn_MovesGeneratedShapes()
    {
        var map = BuildMap(new[] { (0, 3, 0) });
        var solid = map.GenerateCollisionFromClass("Solid");
        var (c, r) = Cell(0, 3);

        map.CenterOn(0f, 0f);

        // Map is 64x64 centered on origin: left edge -32, bottom edge -32; cell (0,0) center = (-24,-24).
        var tile = solid.GetTileAtCell(c, r)!;
        tile.X.ShouldBe(-24f);
        tile.Y.ShouldBe(-24f);
    }

    [Fact]
    public void MapMoved_EveryTrackedCollectionFollows()
    {
        var map = BuildMap(new[] { (0, 3, 0), (1, 3, 1) });
        var solid = map.GenerateCollisionFromClass("Solid");
        var slopes = map.GenerateCollisionFromClass("Slope");
        var hazards = map.GenerateCollisionFromProperty("Missing");

        map.X = 500f;

        solid.X.ShouldBe(500f);
        slopes.X.ShouldBe(500f);
        hazards.X.ShouldBe(500f);
    }

    [Fact]
    public void MapMoved_CollisionResolvesAtNewLocation_NotOld()
    {
        var map = BuildMap(new[] { (0, 3, 0) });
        var solid = map.GenerateCollisionFromClass("Solid");

        map.X = 100f;
        map.Y = 50f;

        // New tile center: (108, 50 - 64 + 8) = (108, -6). Probe overlaps its right edge by 2 units.
        var atNew = new AARect { Width = 4f, Height = 4f, X = 114f, Y = -6f };
        var atOld = new AARect { Width = 4f, Height = 4f, X = 8f, Y = -56f };

        solid.GetSeparationFor(atNew).ShouldNotBe(System.Numerics.Vector2.Zero);
        solid.GetSeparationFor(atOld).ShouldBe(System.Numerics.Vector2.Zero);
    }

    [Fact]
    public void MapMoved_ManualShapesOffset_IsPreserved()
    {
        var map = BuildMap(new[] { (0, 3, 0) });
        var solid = map.GenerateCollisionFromClass("Solid");
        solid.X += 5f; // game nudges the shapes relative to the map

        map.X = 100f;

        solid.X.ShouldBe(105f);
    }

    [Fact]
    public void TryReload_AfterMapMoved_RegeneratesShapesAtMovedPosition()
    {
        var map = BuildMap(new[] { (0, 3, 0) });
        var solid = map.GenerateCollisionFromClass("Solid");
        map.X = 100f;
        map.Y = 50f;

        map.TryReload(BuildTilemap(new[] { (0, 3, 0), (2, 3, 0) })).ShouldBeTrue();

        var (c, r) = Cell(2, 3);
        var added = solid.GetTileAtCell(c, r)!;
        added.X.ShouldBe(100f + 2 * Tile + Tile / 2f);
        added.Y.ShouldBe(50f - 64f + Tile / 2f);
        solid.Rectangles.Count().ShouldBe(2);
    }
}
