using System;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class TileShapeCollectionTests
{
    // ── AddTileAtCell ────────────────────────────────────────────────────────

    [Fact]
    public void AddTileAtCell_LoneTile_HasAllRepositionDirections()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        tiles.GetTileAtCell(0, 0)!.RepositionDirections.ShouldBe(RepositionDirections.All);
    }

    [Fact]
    public void AddTileAtCell_AdjacentHorizontal_InnerEdgesRemoved()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddTileAtCell(1, 0);

        // Left tile: right edge is shared — no Right reposition
        tiles.GetTileAtCell(0, 0)!.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Left);

        // Right tile: left edge is shared — no Left reposition
        tiles.GetTileAtCell(1, 0)!.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Right);
    }

    [Fact]
    public void AddTileAtCell_TilePositionedCorrectly()
    {
        // Cell (2, 3) with GridSize=16 and origin at (100, 200)
        // Center X = 100 + 2*16 + 8 = 140, Center Y = 200 + 3*16 + 8 = 256
        var tiles = new TileShapeCollection { X = 100f, Y = 200f, GridSize = 16f };
        tiles.AddTileAtCell(2, 3);

        var tile = tiles.GetTileAtCell(2, 3)!;
        tile.X.ShouldBe(140f, tolerance: 0.001f);
        tile.Y.ShouldBe(256f, tolerance: 0.001f);
    }

    // ── AddTileAtWorld ───────────────────────────────────────────────────────

    [Fact]
    public void AddTileAtWorld_PositionInsideCell_AddsTileAtThatCell()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtWorld(20f, 5f); // falls in cell (1, 0)

        tiles.GetTileAtCell(1, 0).ShouldNotBeNull();
    }

    // ── Raycast ──────────────────────────────────────────────────────────────

    [Fact]
    public void Raycast_HorizontalRay_HitsLeftFaceOfTile()
    {
        // Tile at cell (2, 0): occupies world X=[32,48], left face at X=32
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(2, 0);

        bool hit = tiles.Raycast(new Vector2(0f, 8f), new Vector2(64f, 8f),
            out Vector2 hitPoint, out Vector2 hitNormal);

        hit.ShouldBeTrue();
        hitPoint.X.ShouldBe(32f, tolerance: 0.001f);
        hitPoint.Y.ShouldBe(8f, tolerance: 0.001f);
        hitNormal.ShouldBe(new Vector2(-1f, 0f));
    }

    [Fact]
    public void Raycast_MultipleHits_ReturnsClosest()
    {
        // Two tiles; ray should stop at the nearer one (cell 1, left face at X=16)
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(1, 0);
        tiles.AddTileAtCell(3, 0);

        bool hit = tiles.Raycast(new Vector2(0f, 8f), new Vector2(80f, 8f),
            out Vector2 hitPoint, out _);

        hit.ShouldBeTrue();
        hitPoint.X.ShouldBe(16f, tolerance: 0.001f);
    }

    [Fact]
    public void Raycast_NoTileOnPath_ReturnsFalse()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 5); // far off the ray path

        bool hit = tiles.Raycast(new Vector2(0f, 8f), new Vector2(64f, 8f),
            out _, out _);

        hit.ShouldBeFalse();
    }

    // ── RemoveTileAtCell ─────────────────────────────────────────────────────

    [Fact]
    public void RemoveTileAtCell_AdjacentTile_NeighborRegainsRepositionDirection()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddTileAtCell(1, 0);
        tiles.RemoveTileAtCell(1, 0);

        // After removing the right neighbor, (0,0) should have All directions again
        tiles.GetTileAtCell(0, 0)!.RepositionDirections.ShouldBe(RepositionDirections.All);
    }

    // ── Clear ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllTiles_AllowsGridPropertyChanges()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddPolygonTileAtCell(1, 0, SquarePrototype());

        tiles.Clear();

        tiles.GetTileAtCell(0, 0).ShouldBeNull();
        tiles.GetPolygonTileAtCell(1, 0).ShouldBeNull();
        // Should not throw after Clear
        tiles.GridSize = 32f;
        tiles.X = 10f;
        tiles.Y = 20f;
    }

    // ── CollidesWith ─────────────────────────────────────────────────────────

    [Fact]
    public void CollidesWith_ShapeOverlappingTile_ReturnsTrue()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0); // tile center at (8, 8), spans [0..16] x [0..16]

        var rect = new AxisAlignedRectangle { Width = 16f, Height = 16f, X = 12f, Y = 8f };

        tiles.CollidesWith(rect).ShouldBeTrue();
    }

    [Fact]
    public void CollidesWith_ShapeNotOverlappingTile_ReturnsFalse()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0); // tile spans [0..16] x [0..16]

        var rect = new AxisAlignedRectangle { Width = 16f, Height = 16f, X = 100f, Y = 100f };

        tiles.CollidesWith(rect).ShouldBeFalse();
    }

    // ── GetSeparationFor ─────────────────────────────────────────────────────

    [Fact]
    public void GetSeparationFor_ShapeOverlappingFromRight_PushesRight()
    {
        // Tile at (0,0): center=(8,8), right edge at X=16
        // Shape overlaps from the right side: its left edge is at 14, center at 22
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        var rect = new AxisAlignedRectangle { Width = 16f, Height = 8f, X = 22f, Y = 8f };

        var sep = tiles.GetSeparationFor(rect);
        sep.X.ShouldBeGreaterThan(0f); // pushed rightward (+X) out of the tile
        sep.Y.ShouldBe(0f, tolerance: 0.001f);
    }

    [Fact]
    public void GetSeparationFor_CornerClip_DiagonallyAdjacentTileDoesNotReposition()
    {
        // Reproduce the corner-clip bug: player walks into the corner between a floor and a wall.
        //
        //   [wall-L][wall-R]
        //   [floor ][diag  ]  ← diag has Down|Right (tiles above and to its left)
        //
        // The diag tile's Right face is exposed, so it can push rightward. But when the player's
        // bottom-right corner barely clips the diag tile's top-left corner, the player center is
        // to the LEFT of the diag tile center — a rightward push would be wrong and cause
        // the player to teleport through the wall.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 1); // wall-L
        tiles.AddTileAtCell(1, 1); // wall-R
        tiles.AddTileAtCell(0, 0); // floor
        tiles.AddTileAtCell(1, 0); // diag — Down|Right (tiles above and to left suppress Up and Left)

        // Verify the diag tile has Down|Right (the scenario precondition)
        tiles.GetTileAtCell(1, 0)!.RepositionDirections.ShouldBe(
            RepositionDirections.Down | RepositionDirections.Right);

        // Player: 14x14, center just to the left of the wall, sitting on the floor.
        // Right edge at X=17 barely clips diag tile left edge at X=16.
        // Player center X=10, diag tile center X=24 → player center is to the LEFT.
        var player = new AxisAlignedRectangle { Width = 14f, Height = 14f, X = 10f, Y = 23f };

        var sep = tiles.GetSeparationFor(player);

        // The diag tile must not push the player right (center check suppresses it).
        sep.X.ShouldBeLessThanOrEqualTo(0f);
    }

    [Fact]
    public void GetSeparationFor_MultipleAdjacentTiles_DoesNotDoubleCount()
    {
        // Two tiles side by side: (0,0) at center=(8,8) and (1,0) at center=(24,8); top edge = 16.
        // Shape spans both tiles with bottom at Y=14, overlapping the top face by 2 px.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddTileAtCell(1, 0);

        // Width=24, Height=8, Y=18 → minY=14, maxY=22; bottom overlaps tile top (16) by 2 px
        var rect = new AxisAlignedRectangle { Width = 24f, Height = 8f, X = 16f, Y = 18f };
        float expectedSep = 2f;

        var sep = tiles.GetSeparationFor(rect);
        // Should push up by exactly 2, not 4 (double-counted from two tiles)
        sep.Y.ShouldBe(expectedSep, tolerance: 0.001f);
    }

    // ── GridSize — throws after tiles added ─────────────────────────────────────

    [Fact]
    public void GridSize_SetAfterTilesAdded_Throws()
    {
        var tiles = new TileShapeCollection();
        tiles.AddTileAtCell(0, 0);

        Should.Throw<InvalidOperationException>(() => tiles.GridSize = 32f);
    }

    // ── X / Y — shifts existing tiles ────────────────────────────────────────────

    [Fact]
    public void X_SetAfterTilesAdded_ShiftsTiles()
    {
        // Cell (0,0) with GridSize=16: tile center starts at (8, 8)
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        tiles.X = 100f;

        tiles.GetTileAtCell(0, 0)!.X.ShouldBe(108f, tolerance: 0.001f);
    }

    [Fact]
    public void Y_SetAfterTilesAdded_ShiftsTiles()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        tiles.Y = 50f;

        tiles.GetTileAtCell(0, 0)!.Y.ShouldBe(58f, tolerance: 0.001f);
    }

    [Fact]
    public void X_SetAfterTilesAdded_ShiftsPolygonTiles()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, SquarePrototype());

        tiles.X = 100f;

        tiles.GetPolygonTileAtCell(0, 0)!.X.ShouldBe(108f, tolerance: 0.001f);
    }

    // ── AddPolygonTileAtCell ──────────────────────────────────────────────────

    private static Polygon SquarePrototype(float halfSize = 8f) => Polygon.FromPoints(new[]
    {
        new Vector2(-halfSize, -halfSize),
        new Vector2( halfSize, -halfSize),
        new Vector2( halfSize,  halfSize),
        new Vector2(-halfSize,  halfSize),
    });

    [Fact]
    public void AddPolygonTileAtCell_GetPolygonTileAtCell_ReturnsTile()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(2, 3, SquarePrototype());

        tiles.GetPolygonTileAtCell(2, 3).ShouldNotBeNull();
    }

    [Fact]
    public void AddPolygonTileAtCell_TilePositionedAtCellCenter()
    {
        // Cell (2,3) center = (2*16+8, 3*16+8) = (40, 56)
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(2, 3, SquarePrototype());

        var poly = tiles.GetPolygonTileAtCell(2, 3)!;
        poly.X.ShouldBe(40f, tolerance: 0.001f);
        poly.Y.ShouldBe(56f, tolerance: 0.001f);
    }

    [Fact]
    public void AddPolygonTileAtCell_DoesNotOverwriteExistingRectTile()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddPolygonTileAtCell(0, 0, SquarePrototype()); // same cell — should be ignored

        tiles.GetPolygonTileAtCell(0, 0).ShouldBeNull();
        tiles.GetTileAtCell(0, 0).ShouldNotBeNull();
    }

    [Fact]
    public void RemovePolygonTileAtCell_RemovedTileNoLongerReturned()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(1, 1, SquarePrototype());
        tiles.RemovePolygonTileAtCell(1, 1);

        tiles.GetPolygonTileAtCell(1, 1).ShouldBeNull();
    }

    // ── GetSeparationFor (polygon tiles) ─────────────────────────────────────

    [Fact]
    public void GetSeparationFor_PolygonTile_PushesRectOutFromRight()
    {
        // Square polygon tile at cell (0,0): world points form a 16×16 square centered at (8,8).
        // Rect overlaps from the right: center X=22, left edge at 14 → 2 px overlap on X.
        // Y overlap is much larger (8 px) so SAT picks X as the minimum axis.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, SquarePrototype());

        var rect = new AxisAlignedRectangle { Width = 16f, Height = 8f, X = 22f, Y = 8f };

        var sep = tiles.GetSeparationFor(rect);
        sep.X.ShouldBeGreaterThan(0f); // pushed rightward out of the polygon
        sep.Y.ShouldBe(0f, tolerance: 0.001f);
    }

    [Fact]
    public void GetSeparationFor_PolygonTile_NoOverlap_ReturnsZero()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, SquarePrototype());

        var rect = new AxisAlignedRectangle { Width = 8f, Height = 8f, X = 100f, Y = 100f };

        tiles.GetSeparationFor(rect).ShouldBe(Vector2.Zero);
    }

    [Fact]
    public void GetSeparationFor_RemovedPolygonTile_ReturnsZero()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, SquarePrototype());
        tiles.RemovePolygonTileAtCell(0, 0);

        var rect = new AxisAlignedRectangle { Width = 16f, Height = 8f, X = 22f, Y = 8f };

        tiles.GetSeparationFor(rect).ShouldBe(Vector2.Zero);
    }

    [Fact]
    public void GetSeparationFor_CircleVsPolygonTile_PushesCircleOut()
    {
        // Square polygon tile at cell (0,0): covers [0..16] x [0..16].
        // Circle at X=18, Y=8, radius=6 — left edge at X=12, overlapping by 4px on X axis.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, SquarePrototype());

        var circle = new Circle { Radius = 6f, X = 18f, Y = 8f };

        var sep = tiles.GetSeparationFor(circle);
        sep.X.ShouldBeGreaterThan(0f); // pushed rightward
        sep.Y.ShouldBe(0f, tolerance: 0.01f);
    }

    // ── Raycast (polygon tiles) ───────────────────────────────────────────────

    [Fact]
    public void Raycast_PolygonTile_HitsLeftFace()
    {
        // Square polygon tile at cell (2,0): center at (40,8), left edge at X=32.
        // Ray travels right from X=10 at Y=8 — should hit the polygon's left edge at (32,8).
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(2, 0, SquarePrototype());

        bool hit = tiles.Raycast(new Vector2(10f, 8f), new Vector2(80f, 8f),
            out Vector2 hitPoint, out Vector2 hitNormal);

        hit.ShouldBeTrue();
        hitPoint.X.ShouldBe(32f, tolerance: 0.1f);
        hitPoint.Y.ShouldBe(8f, tolerance: 0.1f);
        hitNormal.X.ShouldBeLessThan(0f); // normal points back toward start (leftward)
    }

    [Fact]
    public void Raycast_PolygonTile_TriangleSlope_HitsHypotenuse()
    {
        // Right-triangle tile at cell (1,0): lower-left half of cell.
        // World points: (16,0), (32,0), (16,16). Hypotenuse from (32,0) to (16,16).
        // A ray at Y=8 going right enters the cell, the left edge (X=16) is the first hit.
        var prototype = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f), // world (16,0)
            new Vector2( 8f, -8f), // world (32,0)
            new Vector2(-8f,  8f), // world (16,16)
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(1, 0, prototype);

        bool hit = tiles.Raycast(new Vector2(0f, 8f), new Vector2(40f, 8f), out Vector2 hitPoint, out _);

        hit.ShouldBeTrue();
        hitPoint.X.ShouldBe(16f, tolerance: 0.1f); // left edge of the triangle at X=16
        hitPoint.Y.ShouldBe(8f, tolerance: 0.1f);
    }

    [Fact]
    public void Raycast_PolygonTile_RayDoesNotReachCell_ReturnsFalse()
    {
        // Polygon tile at cell (5,0) — ray stops well before it.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(5, 0, SquarePrototype());

        bool hit = tiles.Raycast(new Vector2(0f, 8f), new Vector2(40f, 8f), out _, out _);

        hit.ShouldBeFalse();
    }
}
