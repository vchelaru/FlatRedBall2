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
}
