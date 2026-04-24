using System;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class TileShapeCollectionTests
{
    // ── GetCellWorldPosition ─────────────────────────────────────────────────

    [Fact]
    public void GetCellWorldPosition_ReturnsCellCenter_InverseOfGetCellAt()
    {
        var tiles = new TileShapeCollection { GridSize = 16f, X = 100f, Y = -50f };

        // Cell (2, 3): bottom-left at (100 + 32, -50 + 48) = (132, -2); center = (140, 6)
        tiles.GetCellWorldPosition(2, 3).ShouldBe(new Vector2(140f, 6f));

        // Round-trip: GetCellAt of the center should recover the same cell
        var center = tiles.GetCellWorldPosition(2, 3);
        tiles.GetCellAt(center).ShouldBe((2, 3));
    }

    [Fact]
    public void GetCellWorldPosition_NegativeCells_OK()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.GetCellWorldPosition(-1, -1).ShouldBe(new Vector2(-8f, -8f));
    }

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

    // ── GetSeparationFor — opposite-direction Y accumulation ─────────────────

    [Fact]
    public void GetSeparationFor_TallBodySpansLowerAndUpperTile_ReturnsPositiveY()
    {
        // Repro for "player falls through cloud when tall body overlaps two tiles."
        // Body is tall enough to overlap the lower tile from above (+2 upward push)
        // AND the upper tile from below (-6 downward push).
        // Bug: GetSeparationFor takes the largest absolute Y push — -6 beats +2, so
        // sep.Y is negative, the one-way gate's first check (sep.Y <= 0) rejects the
        // collision, and the player falls through.
        // Fix: an established Y push direction must not be overridden by an opposite push.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0); // bottom: Y=[0,16], top at y=16
        tiles.AddTileAtCell(0, 2); // upper: Y=[32,48], bottom at y=32 (one-cell gap at row 1)

        // Body: bottom=14 (2 units inside lower tile), top=38 (6 units inside upper tile).
        var entity = new Entity { X = 8f, Y = 14f };
        var shape = new AxisAlignedRectangle { Width = 12f, Height = 24f, Y = 12f };
        entity.Add(shape);

        var sep = tiles.GetSeparationFor(shape);

        // Positive Y (push up) must win — the lower tile's +2 push should not be
        // replaced by the upper tile's -6 push.
        sep.Y.ShouldBeGreaterThan(0f);
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

    // ── Polygon SuppressedEdges ─────────────────────────────────────────────

    [Fact]
    public void AddPolygonTileAtCell_AdjacentRect_SuppressesRectRepositionDirection()
    {
        // Rect tile at (0,0), polygon tile at (1,0).
        // The rect's Right direction should be suppressed because of the adjacent polygon.
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddPolygonTileAtCell(1, 0, slope);

        var rect = tiles.GetTileAtCell(0, 0)!;
        rect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Left,
            "rect adjacent to polygon should suppress the shared direction");
    }

    [Fact]
    public void AddPolygonTileAtCell_AdjacentRectToRight_SuppressesSharedEdge()
    {
        // Right-triangle slope at cell (0,0): points (-8,-8),(8,-8),(8,8).
        // Edge 1 connects (8,-8)→(8,8) — the right side, along the right cell boundary.
        // Rect tile at cell (1,0) makes that edge interior.
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, slope);
        tiles.AddTileAtCell(1, 0);

        var poly = tiles.GetPolygonTileAtCell(0, 0)!;
        // Edge 1 (right side) should be suppressed
        (poly.SuppressedEdges & (1 << 1)).ShouldNotBe(0,
            "edge along shared boundary should be suppressed");
    }

    [Fact]
    public void AddPolygonTileAtCell_NoNeighbor_NoSuppressedEdges()
    {
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, slope);

        var poly = tiles.GetPolygonTileAtCell(0, 0)!;
        poly.SuppressedEdges.ShouldBe(0, "lone polygon tile should have no suppressed edges");
    }

    [Fact]
    public void RemoveTileAtCell_NeighborRemoved_PolygonEdgeRestored()
    {
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, slope);
        tiles.AddTileAtCell(1, 0);
        tiles.RemoveTileAtCell(1, 0);

        var poly = tiles.GetPolygonTileAtCell(0, 0)!;
        poly.SuppressedEdges.ShouldBe(0,
            "removing neighbor should restore all polygon edges");
    }

    [Fact]
    public void AddTileAtCell_NextToExistingPolygon_SuppressesPolygonEdge()
    {
        // Add polygon first, then add rect neighbor — polygon should update.
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2(-8f,  8f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(1, 0, slope);
        // Edge 0 connects (-8,-8)→(8,-8) — bottom side, along bottom cell boundary.
        // Adding a rect tile below should suppress it.
        tiles.AddTileAtCell(1, -1);

        var poly = tiles.GetPolygonTileAtCell(1, 0)!;
        (poly.SuppressedEdges & (1 << 0)).ShouldNotBe(0,
            "bottom edge should be suppressed when neighbor is added below");
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
    public void AddPolygonTileAtCell_DuplicatePolygonCell_Throws()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, SquarePrototype());

        Should.Throw<InvalidOperationException>(
            () => tiles.AddPolygonTileAtCell(0, 0, SquarePrototype()));
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
    public void Raycast_HitShapeOverload_FullCellTile_ReturnsThatRect()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(2, 0);
        var expected = tiles.GetTileAtCell(2, 0);

        bool hit = tiles.Raycast(new Vector2(0f, 8f), new Vector2(64f, 8f),
            out _, out _, out ICollidable? hitShape);

        hit.ShouldBeTrue();
        hitShape.ShouldBeSameAs(expected);
    }

    [Fact]
    public void Raycast_HitShapeOverload_PolygonTile_ReturnsPolygonInstance()
    {
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(2, 0, SquarePrototype());

        bool hit = tiles.Raycast(new Vector2(0f, 8f), new Vector2(80f, 8f),
            out _, out _, out ICollidable? hitShape);

        hit.ShouldBeTrue();
        hitShape.ShouldBeOfType<Polygon>();
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

    [Fact]
    public void Raycast_PolygonTile_StartsInsideCellAbovePolygon_HitsPolygonTopSurface()
    {
        // 45° slope polygon filling bottom-right triangle of cell (0,0):
        // world verts (0,0), (16,0), (16,16). Slope surface y = x along the hypotenuse.
        // Ray starts inside cell at (12, 15) — above the surface (surface at x=12 is y=12).
        // Probes straight down to y=-1. Expected: hits slope surface at y=12.
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f), // world (0,0)
            new Vector2( 8f, -8f), // world (16,0)
            new Vector2( 8f,  8f), // world (16,16)
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, slope);

        bool hit = tiles.Raycast(new Vector2(12f, 15f), new Vector2(12f, -1f),
            out Vector2 hitPoint, out _, out ICollidable? hitShape);

        hit.ShouldBeTrue();
        hitPoint.Y.ShouldBe(12f, tolerance: 0.1f);
        hitShape.ShouldBeOfType<Polygon>();
    }

    [Fact]
    public void Raycast_PolygonTile_StartsEmbeddedSlightlyBelowSurface_HitsPolygonNotCellBelow()
    {
        // 45° slope polygon filling bottom-right triangle of cell (0,1) at y ∈ [16,32]:
        // world verts (0,16), (16,16), (16,32). Surface y = x + 16 along hypotenuse.
        // Below (cell (0,0), y∈[0,16]) is a full-cell rect — a "floor below the slope".
        // Ray feet at x=12 — surface there is y=28. Start ray slightly EMBEDDED at y=27.9,
        // probe down to y=12. Without fix: start cell's polygon is skipped, ray continues
        // down and hits the full-cell tile's top at y=16. With fix: returns a polygon hit
        // at (or near) the start position — NOT the cell below.
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f), // world (0,16)
            new Vector2( 8f, -8f), // world (16,16)
            new Vector2( 8f,  8f), // world (16,32)
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 1, slope);
        tiles.AddTileAtCell(0, 0); // full-cell rect at y∈[0,16] — the "wrong" thing to hit

        bool hit = tiles.Raycast(new Vector2(12f, 27.9f), new Vector2(12f, 12f),
            out Vector2 hitPoint, out _, out ICollidable? hitShape);

        hit.ShouldBeTrue();
        hitShape.ShouldBeOfType<Polygon>();       // must be the slope, not the rect below
        hitPoint.Y.ShouldBeGreaterThan(16f);      // above the cell-below boundary
    }

    // ── Raycast (sub-cell rects) ─────────────────────────────────────────────

    [Fact]
    public void Raycast_DownwardRay_HitsSubCellRect()
    {
        // Cell (0,0) contains a 16x8 bottom-half rect — top face at y=8.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddRectangleTileAtCell(0, 0, 0f, -4f, 16f, 8f);

        bool hit = tiles.Raycast(new Vector2(8f, 20f), new Vector2(8f, -4f),
            out Vector2 hitPoint, out Vector2 hitNormal);

        hit.ShouldBeTrue();
        hitPoint.Y.ShouldBe(8f, tolerance: 0.001f);
        hitNormal.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Raycast_SubCellRectCloserThanPolygon_ReturnsSubCellHit()
    {
        // Cell (0,0): polygon bottom half (top y=8) AND sub-cell rect filling top quarter
        // (y=[12,16], top face at y=16). A downward ray from above hits the rect (y=16) first.
        var tiles = new TileShapeCollection { GridSize = 16f };
        var bottomHalf = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  0f),
            new Vector2(-8f,  0f),
        });
        tiles.AddPolygonTileAtCell(0, 0, bottomHalf);
        tiles.AddRectangleTileAtCell(0, 0, 0f, 6f, 16f, 4f); // center y=14, height 4 → top y=16

        bool hit = tiles.Raycast(new Vector2(8f, 30f), new Vector2(8f, -4f),
            out Vector2 hitPoint, out _);

        hit.ShouldBeTrue();
        hitPoint.Y.ShouldBe(16f, tolerance: 0.001f);
    }

    [Fact]
    public void Raycast_MultipleSubCellRectsInCell_ReturnsEarliestHit()
    {
        // Two rects in cell (0,0): lower (top y=4) and upper (top y=12).
        // Downward ray hits the upper one first.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddRectangleTileAtCell(0, 0, 0f, -6f, 16f, 4f); // top y=4
        tiles.AddRectangleTileAtCell(0, 0, 0f,  2f, 16f, 4f); // top y=12

        bool hit = tiles.Raycast(new Vector2(8f, 30f), new Vector2(8f, -4f),
            out Vector2 hitPoint, out _);

        hit.ShouldBeTrue();
        hitPoint.Y.ShouldBe(12f, tolerance: 0.001f);
    }

    [Fact]
    public void Raycast_SubCellRectOffAxis_RayContinuesToNextCell()
    {
        // Cell (0,0) has a sub-cell rect only in its left half (x=[0,8]).
        // Cell (1,0) has a full tile. A downward ray at x=12 should skip the rect and
        // continue — ultimately not hitting anything directly below in its column.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddRectangleTileAtCell(0, 0, -4f, 0f, 8f, 16f); // left half of cell (0,0)
        tiles.AddTileAtCell(2, 0); // a separate tile, not under the ray

        bool hit = tiles.Raycast(new Vector2(12f, 20f), new Vector2(12f, -4f),
            out _, out _);

        hit.ShouldBeFalse();
    }

    // ── PlatformerFloor slope collision ──────────────────────────────────────

    // Right-triangle slope going up-right: bottom spans the full cell width,
    // hypotenuse rises from bottom-left to top-right.
    // Surface height at any X within the cell = linear interpolation from 0 to GridSize.
    private static Polygon UpRightSlope(float halfSize = 8f) => Polygon.FromPoints(new[]
    {
        new Vector2(-halfSize, -halfSize), // bottom-left
        new Vector2( halfSize, -halfSize), // bottom-right
        new Vector2( halfSize,  halfSize), // top-right
    });

    [Fact]
    public void GetSeparationFor_PlatformerFloor_SlopeRamp_PushesUpVertically()
    {
        // Up-right slope at cell (0,0): center at (8,8).
        // World points: (0,0), (16,0), (16,16). Hypotenuse from (0,0) to (16,16).
        // Rect centered at X=12, bottom at Y=6 (center Y=10, height=8).
        // Surface height at X=12: lerp from 0 to 16 over [0..16] → 12.
        // Rect bottom is 6 < 12 → push up by 6.
        float expectedSepY = 6f;
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, UpRightSlope());

        var rect = new AxisAlignedRectangle { Width = 8f, Height = 8f, X = 12f, Y = 10f };

        var sep = tiles.GetSeparationFor(rect, SlopeCollisionMode.PlatformerFloor);

        sep.X.ShouldBe(0f, tolerance: 0.01f, customMessage: "platformer slope should not push horizontally");
        sep.Y.ShouldBe(expectedSepY, tolerance: 0.1f);
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_SlopeAdjacentToRect_NoSnagging()
    {
        // Floor rect at cell (0,0), up-right slope at cell (1,0).
        // Rect tile top edge at Y=16. Slope surface at its left edge (X=16) = 0 + cell bottom = 0.
        // Wait — slope bottom-left is at Y=0, so surface at X=16 is 0. That doesn't connect.
        // For a smooth transition, the slope should start at the floor's top edge.
        // Using cell (0,0) as floor and cell (1,0) as slope:
        //   Floor top = row 0 top = 16.
        //   Slope at cell (1,0): world points (16,0),(32,0),(32,16). Surface at X=16 = 0.
        // This slope sits beside the floor, not on top. For a ramp going up FROM the floor,
        // the slope should be at row 1 (one above ground). But for snagging test, we need
        // the player to transition from the rect tile onto the slope at the seam.
        //
        // Simpler setup: rect at (0,0) with top at Y=16. Slope at (1,0) with surface
        // starting at Y=0 at X=16. Player rect at the seam: X=17, bottom at Y=6.
        // Floor rect pushes up to Y=16 (player is above floor). Slope surface at X=17:
        // lerp 0→16 over [16..32] → (17-16)/(32-16) * 16 = 1. Player bottom 6 > 1 → no slope push.
        // No snagging because the slope pushes vertically, not horizontally.
        //
        // Better test: player straddles the seam. X=15 (inside floor rect cell), bottom at Y=14.
        // Floor rect (cell 0,0): spans [0..16] x [0..16]. Player left=11, right=19.
        // Player overlaps floor rect: top of floor = 16, player bottom = 14 → push up 2.
        // Player also overlaps slope cell (1,0): center X=15, but slope cell is [16..32].
        // Player doesn't overlap slope cell. Need player further right.
        //
        // Player at X=20 (inside slope cell), bottom at Y=2 (center Y=6, height=8).
        // Slope at cell (1,0): surface at X=20 → lerp (20-16)/(32-16) * 16 = 4.
        // Player bottom=2 < 4 → push up by 2. No X push. No snagging.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddPolygonTileAtCell(1, 0, UpRightSlope());

        // Player overlapping the slope tile near the seam with the rect tile.
        var rect = new AxisAlignedRectangle { Width = 8f, Height = 8f, X = 20f, Y = 6f };

        var sep = tiles.GetSeparationFor(rect, SlopeCollisionMode.PlatformerFloor);

        sep.X.ShouldBe(0f, tolerance: 0.01f, customMessage: "slope should not push horizontally at seam");
        sep.Y.ShouldBeGreaterThan(0f, "slope should push player up to surface");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_NoOverlap_ReturnsZero()
    {
        // Rect above the slope surface — no separation needed.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, UpRightSlope());

        // Slope at cell (0,0): surface at X=8 → 8. Rect bottom at Y=20 → well above.
        var rect = new AxisAlignedRectangle { Width = 8f, Height = 8f, X = 8f, Y = 24f };

        tiles.GetSeparationFor(rect, SlopeCollisionMode.PlatformerFloor).ShouldBe(Vector2.Zero);
    }

    [Fact]
    public void GetSeparationFor_Standard_SlopeRamp_UsesSat()
    {
        // Default Standard mode: polygon tiles use SAT, which may have X component.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, UpRightSlope());

        // Same geometry as the PlatformerFloor test — SAT should produce a non-zero MTV.
        var rect = new AxisAlignedRectangle { Width = 8f, Height = 8f, X = 12f, Y = 10f };

        var sep = tiles.GetSeparationFor(rect);
        sep.ShouldNotBe(Vector2.Zero, "Standard mode should use SAT and produce separation");
    }

    // ── PlatformerFloor — preferential landing (velocity-based) ────────────

    // Helper: creates an Entity with a child collision box, positioned and with velocity set.
    private static AxisAlignedRectangle MakePlayerBox(
        float x, float y, float width, float height, float velocityY)
    {
        var entity = new Entity();
        entity.VelocityY = velocityY;
        var box = new AxisAlignedRectangle { Width = width, Height = height };
        entity.Add(box);
        entity.X = x;
        entity.Y = y;
        return box;
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_FallingOntoLedgeEdge_LandsOnTop()
    {
        // Player falls fast (VelocityY=-500), left edge barely clips a platform.
        // Standard AABB: X overlap (4) < Y overlap (3) → pushes up (already correct
        // for this penetration). But center X is outside rect → center-X suppression
        // would strip vertical. With velocity check, lastBottom = 13 - (-500/60) =
        // 13 + 8.33 = 21.33 > rectTop(16) → was above → landing fires.
        // Platform at cell (5, 0): spans [80..96] x [0..16].
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(5, 0);

        // Player center X=78, bottom at Y=13. Spans [72..84] x [13..37].
        // X overlap: 84-80=4. Y overlap: 16-13=3. Standard: Y(3) < X(4) → push up.
        // Center X=78 < rectLeft=80 → outside → suppressed. Landing must restore it.
        var box = MakePlayerBox(78f, 25f, 12f, 24f, velocityY: -500f);

        var sep = tiles.GetSeparationFor(box, SlopeCollisionMode.PlatformerFloor);

        sep.Y.ShouldBeGreaterThan(0f, "falling player should land on top");
        sep.X.ShouldBe(0f, tolerance: 0.01f, customMessage: "should not push horizontally when landing");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_WalkIntoSingleWall_PushesHorizontally()
    {
        // Player walks into wall (VelocityY = 0). Same geometry as landing test but
        // not falling → should push horizontally, not snap up.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        var box = MakePlayerBox(19f, 22f, 12f, 24f, velocityY: 0f);

        var sep = tiles.GetSeparationFor(box, SlopeCollisionMode.PlatformerFloor);

        sep.X.ShouldBeGreaterThan(0f, "should push right away from wall");
        sep.Y.ShouldBe(0f, tolerance: 0.01f, customMessage: "should not push up when hitting wall from side");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_WalkIntoTallWallWithGravity_PushesHorizontally()
    {
        // Player walks right into a 2-tile-tall wall. Gravity gives VelocityY ≈ -15.
        // Player center X is to the LEFT of the wall (approaching from the left side).
        // Should push horizontally, NOT pop up onto the wall.
        //
        // Wall at col 3, rows 0-1: spans [48..64] x [0..32].
        // Player center X=46, spans [40..52] x [6..30].
        // Wall bottom tile (3,0): [48..64] x [0..16]. X overlap: 52-48=4. Y overlap: 16-6=10.
        // Wall top tile (3,1): [48..64] x [16..32]. X overlap: 52-48=4. Y overlap: 32-6=26? No...
        // Actually player top = 30, so overlap with (3,1): min(30,32)-max(6,16) = 30-16=14.
        // Standard: X(4) < Y(14 or 10) → push left. Correct without PlatformerFloor.
        // With VelocityY=-15, lastBottom ≈ 6+0.25 = 6.25 < wall top (32) → not above.
        // So PlatformerFloor landing should NOT fire → horizontal push preserved.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(3, 0);
        tiles.AddTileAtCell(3, 1);

        var box = MakePlayerBox(46f, 18f, 12f, 24f, velocityY: -15f);

        var sep = tiles.GetSeparationFor(box, SlopeCollisionMode.PlatformerFloor);

        sep.X.ShouldBeLessThan(0f, "should push left out of wall, not pop up onto it");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_LandingOnInteriorTile_DoesNotFire()
    {
        // Preferential landing must not fire on a tile whose Up direction is suppressed
        // (a tile with another tile directly above it — its top face isn't a surface).
        // Without this check, a player walking past stacked ground tiles can get stuck
        // via tiny upward pushes from interior rows.
        //
        // Setup: single column of 2 stacked tiles (neighbor on right to suppress Right
        // of the lower tile). Lower tile (0, 0) has Up suppressed by (0, 1).
        // Player barely overlaps (0, 0) from the right, slightly below its top.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddTileAtCell(0, 1);

        // Player box right edge at 16.1 (tiny X overlap with (0, 0) at [0..16]).
        // Box Y [14..38] — bottom slightly below (0, 0) top (Y=16).
        // Raw standard AABB: X overlap (0.1) < Y overlap (2). Push left by 0.1.
        // Preferential landing would fire (VelocityY=-15, lastBottom=14.25 > Y=16? NO)
        // Actually let me recompute: lastBottom = 14 - (-15)/60 = 14.25. rectTop=16.
        // 14.25 > 16? NO. Landing wouldn't fire here.
        //
        // Need different geometry: lastBottom > rectTop. Push player closer to top.
        // Box bottom at 15.9. lastBottom = 15.9 + 0.25 = 16.15 > 16. Landing fires.
        // We DON'T want it to fire because (0, 0) has Up suppressed.
        var box = MakePlayerBox(10f + 6.1f, 15.9f + 12f, 12f, 24f, velocityY: -15f);

        var sep = tiles.GetSeparationFor(box, SlopeCollisionMode.PlatformerFloor);

        // Should push LEFT (horizontal), not UP. Only (0, 0) is being checked here
        // (single-column setup). If landing fires, sep.Y > 0. If not, sep.X < 0.
        sep.Y.ShouldBe(0f, tolerance: 0.01f, customMessage: "should not convert to vertical push when tile's Up is suppressed");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_StandingOnGround_GetsPushedUp()
    {
        // Player standing on a wide ground area. Box has sunk slightly into the ground
        // due to gravity. Standard AABB picks horizontal (smaller overlap) but with
        // RepositionDirections only Up allowed, falls through to push Up — correctly
        // restoring the player to the ground surface.
        //
        // Wall-press suppression must NOT strip this vertical push just because the
        // player has slight oscillating VelocityX.
        //
        // Ground at row 2, cols 0-5. Player at col 2-3 area.
        // Tile (2,2): [32..48] x [32..48]. Player at X=42, box [36..48].
        // Player sunk slightly: box Y [32..56] (row 2 bottom to above).
        var tiles = new TileShapeCollection { GridSize = 16f };
        for (int c = 0; c < 6; c++)
        {
            tiles.AddTileAtCell(c, 0);
            tiles.AddTileAtCell(c, 1);
            tiles.AddTileAtCell(c, 2);
        }

        // Player box bottom at Y=32 (bottom of row 2 ground). Should be pushed up to Y=48.
        var box = MakePlayerBox(42f, 32f + 12f, 12f, 24f, velocityY: -15f);
        ((Entity)((IAttachable)box).Parent!).VelocityX = -0.14f; // tiny negative X velocity

        var sep = tiles.GetSeparationFor(box, SlopeCollisionMode.PlatformerFloor);

        sep.Y.ShouldBeGreaterThan(10f, "player sunk into ground should be pushed up strongly");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_WalkRightIntoPitWall_PushesLeft()
    {
        // Player on ground walks right, falls into gap, hits the wall on the other side.
        // VelocityX > 0 (holding right), VelocityY < 0 (gravity while in gap).
        // Ground: cols 0-4 row 0. Gap: cols 5-6. Wall: col 7 rows 0-2.
        // Player fell slightly below ground level, right edge clips wall.
        var tiles = new TileShapeCollection { GridSize = 16f };
        for (int c = 0; c < 5; c++) tiles.AddTileAtCell(c, 0);
        tiles.AddTileAtCell(7, 0);
        tiles.AddTileAtCell(7, 1);
        tiles.AddTileAtCell(7, 2);

        // Player center X=110, width=12, spans [104..116]. Wall col 7: [112..128].
        // X overlap: 116-112=4. Player bottom at Y=12, wall tile (7,0) top=16. Y overlap=4.
        // VelocityX=150 (walking right), VelocityY=-15 (gravity).
        var box = MakePlayerBox(110f, 24f, 12f, 24f, velocityY: -15f);
        // Also set VelocityX on the parent entity
        ((Entity)((IAttachable)box).Parent!).VelocityX = 150f;

        var sep = tiles.GetSeparationFor(box, SlopeCollisionMode.PlatformerFloor);

        sep.X.ShouldBeLessThan(0f, "walking right into wall should push left");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_FallingAlongPitWall_PushesHorizontally()
    {
        // Player falls down a pit, sliding along the vertical wall face.
        // VelocityY is large negative (falling). The player clips the wall tiles
        // from the side. Even though they're falling, they should be pushed
        // horizontally — they were BESIDE the wall last frame, not ABOVE it.
        //
        // Pit wall at col 5, rows 0-2. Player to the left, falling along the face.
        // Player center X=78, spans [72..84]. Wall [80..96].
        // Player was to the LEFT of the wall last frame AND this frame.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(5, 0);
        tiles.AddTileAtCell(5, 1);
        tiles.AddTileAtCell(5, 2);

        // Falling fast, clipping the top wall tile from the side.
        // Player bottom at Y=34, wall tile (5,2) top=48. lastBottom = 34+500/60 ≈ 42.3.
        // 42.3 < 48 → lastBottom was NOT above tile top. Landing shouldn't fire.
        // But what about tile (5,1) top=32? lastBottom 42.3 > 32 → WAS above tile (5,1)!
        // That's the bug: landing fires on lower tiles because lastBottom > their top.
        // Fix: also check that the player was horizontally overlapping (above) the tile
        // last frame, not beside it. lastCenterX should be within rect's X span.
        var box = MakePlayerBox(78f, 46f, 12f, 24f, velocityY: -500f);

        var sep = tiles.GetSeparationFor(box, SlopeCollisionMode.PlatformerFloor);

        sep.X.ShouldBeLessThan(0f, "falling along pit wall should push left, not snap onto tiles");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_ShallowSlope_SurfaceHeightIsHalfTile()
    {
        // Shallow slope rising half a tile over one cell width.
        // Points: bottom-left (-8,-8), bottom-right (8,-8), mid-right (8,0).
        // At cell (0, 0): world points (0,0), (16,0), (16,8). Surface at X=8 = 4.
        var shallow = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  0f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, shallow);

        var rect = new AxisAlignedRectangle { Width = 4f, Height = 8f, X = 8f, Y = 4f };

        var sep = tiles.GetSeparationFor(rect, SlopeCollisionMode.PlatformerFloor);

        sep.Y.ShouldBe(4f, tolerance: 0.1f, customMessage: "shallow slope at center should push up to half-tile height");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_AdjacentSlopesDiagonal_SurfaceContinuous()
    {
        // Two up-right slopes stacked diagonally: (0,0) and (1,1).
        // At the seam X=16, both tiles' surface height meet at Y=16.
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, slope);
        tiles.AddPolygonTileAtCell(1, 1, slope);

        // Rect near seam (X=15, just inside cell 0's top-right). Surface should be ~15.
        var rect = new AxisAlignedRectangle { Width = 4f, Height = 8f, X = 15f, Y = 12f };

        var sep = tiles.GetSeparationFor(rect, SlopeCollisionMode.PlatformerFloor);

        sep.X.ShouldBe(0f, tolerance: 0.01f, customMessage: "slopes should not push horizontally");
        sep.Y.ShouldBeGreaterThan(0f, "should push up onto seam between adjacent slopes");
    }

    // V-flipped up-right slope: a "ceiling" polygon whose solid mass sits in the upper
    // half of the cell. Hypotenuse runs from top-left to bottom-right, open space below.
    private static Polygon CeilingSlope(float halfSize = 8f) => Polygon.FromPoints(new[]
    {
        new Vector2(-halfSize,  halfSize), // top-left
        new Vector2( halfSize,  halfSize), // top-right
        new Vector2( halfSize, -halfSize), // bottom-right
    });

    [Fact]
    public void GetSeparationFor_PlatformerFloor_CeilingPolygonFromBelow_DoesNotPushUp()
    {
        // Player jumping up into a V-flipped slope ceiling. The polygon's mass is in the
        // upper half of the cell; open space is below. Heightmap separation (which assumes
        // a floor surface) would push the player UP into/through the ceiling — wrong.
        // Fix: ceiling-like polygons should fall back to SAT, which pushes down/horizontal.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, CeilingSlope());

        // Cell (0,0) spans [0..16] x [0..16]. Ceiling polygon occupies upper region —
        // hypotenuse runs (0,16)→(16,0) with mass above the line.
        // Player jumping up: center X=8, Y=2, W=H=8 → bounds [4..12] x [-2..6].
        // Top of player (Y=6) pokes into the ceiling's lower tip. A floor-style heightmap
        // push would shove the player up to surfaceY=16 (teleport). SAT pushes along the
        // hypotenuse's outward normal, away from the ceiling (down/left).
        var rect = new AxisAlignedRectangle { Width = 8f, Height = 8f, X = 8f, Y = 2f };

        var sep = tiles.GetSeparationFor(rect, SlopeCollisionMode.PlatformerFloor);

        sep.Y.ShouldBeLessThanOrEqualTo(0f, "ceiling polygon must not push the player upward");
    }

    [Fact]
    public void GetSeparationFor_PlatformerFloor_StandingOnEdge_DoesNotSink()
    {
        // Player standing at the right edge of a platform, center X slightly past rect edge.
        // No adjacent slope tile. Should still push up (not suppressed).
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        var player = new AxisAlignedRectangle { Width = 12f, Height = 24f, X = 17f, Y = 27f };

        var sep = tiles.GetSeparationFor(player, SlopeCollisionMode.PlatformerFloor);

        sep.Y.ShouldBeGreaterThan(0f, "standing on edge should push up even when center is past edge");
    }

    // ── Relationship-level SlopeMode (per-relationship resolution) ───────────

    [Fact]
    public void RunCollisions_RelationshipSlopeModePlatformerFloor_PushesPlayerUpSlope()
    {
        // Player entity vs. a TileShapeCollection containing one floor slope polygon.
        // Relationship.SlopeMode = PlatformerFloor → heightmap path → vertical push only.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, UpRightSlope());

        var player = new Entity();
        var box = new AxisAlignedRectangle { Width = 8f, Height = 8f };
        player.Add(box);
        player.X = 12f; player.Y = 10f; // matches SlopeRamp_PushesUpVertically geometry

        var rel = new CollisionRelationship<Entity, TileShapeCollection>(
            new[] { player }, new[] { tiles });
        rel.SlopeMode = SlopeCollisionMode.PlatformerFloor;
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        player.X.ShouldBe(12f, tolerance: 0.01f, customMessage: "PlatformerFloor must not push horizontally on slope");
        player.Y.ShouldBeGreaterThan(10f, "PlatformerFloor should push player up onto slope surface");
    }

    [Fact]
    public void RunCollisions_RelationshipSlopeModeStandard_UsesSatOnSameSlopeTiles()
    {
        // Same TileShapeCollection as above but relationship has default Standard mode →
        // SAT separation with a non-zero X component (proves the collection isn't globally biased).
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, UpRightSlope());

        var ball = new Entity();
        var circle = new Circle { Radius = 4f };
        ball.Add(circle);
        ball.X = 12f; ball.Y = 10f;

        var rel = new CollisionRelationship<Entity, TileShapeCollection>(
            new[] { ball }, new[] { tiles });
        // No SlopeMode set → defaults to Standard.
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        // SAT pushes along the hypotenuse normal (up-left), so both axes move — unlike
        // PlatformerFloor which would only move Y.
        MathF.Abs(ball.X - 12f).ShouldBeGreaterThan(0.1f, "Standard SAT should produce horizontal component");
    }

    [Fact]
    public void GetSeparationVector_PublicEntryPoint_DefaultsToStandardMode()
    {
        // Calling the ICollidable-level GetSeparationVector on the collection (no relationship)
        // must use Standard mode — the safe symmetric default.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, UpRightSlope());

        var rect = new AxisAlignedRectangle { Width = 8f, Height = 8f, X = 12f, Y = 10f };

        var sep = tiles.GetSeparationVector(rect);

        // Standard mode on this slope produces an SAT MTV with a horizontal component;
        // PlatformerFloor would zero out X.
        MathF.Abs(sep.X).ShouldBeGreaterThan(0.01f, "public entry point should default to Standard SAT");
    }

    // ── AddRectangleTileAtCell — sub-cell rect adjacency ────────────────────

    [Fact]
    public void AddRectangleTileAtCell_AdjacentBottomHalfRects_SuppressSharedInnerFaces()
    {
        // Two 16x8 bottom-half sub-cell rects in cells (0,0) and (1,0) form a continuous curb.
        // Cell (0,0) center is (8, 8); bottom-half rect center is (8, 4). Cell (1,0) bottom-half center (24, 4).
        // Shared face: x=16, y in [0,8]. Left rect's Right face and right rect's Left face should be cleared.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddRectangleTileAtCell(0, 0, 0f, -4f, 16f, 8f);
        tiles.AddRectangleTileAtCell(1, 0, 0f, -4f, 16f, 8f);

        var leftRect  = tiles.GetRectangleTilesAtCell(0, 0)[0];
        var rightRect = tiles.GetRectangleTilesAtCell(1, 0)[0];

        leftRect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Left);
        rightRect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Right);
    }

    [Fact]
    public void AddRectangleTileAtCell_AdjacentFullCellTile_SuppressesSharedFace()
    {
        // Sub-cell bottom-half rect at (0,0): faces left=0, right=16, bottom=0, top=8.
        // Full-cell tile at (1,0): faces left=16, right=32, bottom=0, top=16.
        // Shared face: x=16, y in [0,8] ⊂ [0,16]. Sub-cell rect's Right should be cleared.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddRectangleTileAtCell(0, 0, 0f, -4f, 16f, 8f);
        tiles.AddTileAtCell(1, 0);

        var subRect = tiles.GetRectangleTilesAtCell(0, 0)[0];

        subRect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Left);
    }

    [Fact]
    public void AddRectangleTileAtCell_NonAlignedNeighborRects_NoSuppression()
    {
        // Bottom-half in (0,0): y ∈ [0,8], right face at x=16. Top-half in (1,0): y ∈ [8,16],
        // left face at x=16. Opposite faces are aligned on x but their y-ranges touch only at a
        // single point (y=8), which is zero overlap — must NOT suppress.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddRectangleTileAtCell(0, 0, 0f, -4f, 16f, 8f); // bottom-half in (0,0)
        tiles.AddRectangleTileAtCell(1, 0, 0f,  4f, 16f, 8f); // top-half in (1,0)

        var leftRect  = tiles.GetRectangleTilesAtCell(0, 0)[0];
        var rightRect = tiles.GetRectangleTilesAtCell(1, 0)[0];

        leftRect.RepositionDirections.ShouldBe(RepositionDirections.All);
        rightRect.RepositionDirections.ShouldBe(RepositionDirections.All);
    }

    [Fact]
    public void AddRectangleTileAtCell_FlatRectOnTopOfFullCell_SuppressesFullCellUpFace()
    {
        // Full-cell tile at (0,0): top face y=16, x in [0,16].
        // Bottom-half sub-cell rect at (0,1) center=(8,20), so bottom=16, x in [0,16].
        // Rect's bottom face fully covers the full-cell's top face → both must be suppressed
        // at the seam so a mover crossing from off-the-square onto the flat rect sees a clean surface.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddRectangleTileAtCell(0, 1, 0f, -4f, 16f, 8f);

        var square  = tiles.GetTileAtCell(0, 0)!;
        var subRect = tiles.GetRectangleTilesAtCell(0, 1)[0];

        square.RepositionDirections.ShouldBe(
            RepositionDirections.Left | RepositionDirections.Right | RepositionDirections.Down);
        subRect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Left | RepositionDirections.Right);
    }

    [Fact]
    public void AddRectangleTileAtCell_PartialCoverageAgainstFullCell_LeavesFullCellFaceLive()
    {
        // Full-cell wall at (1,0): left face x=16, y in [0,16].
        // Sub-cell bottom-half rect at (0,0) center=(8,4), right face x=16, y in [0,8].
        // Rect only covers bottom half of the wall's left face — wall's Left must stay live
        // (regression guard: a tall wall next to a short spike should still repo movers off the wall).
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(1, 0);
        tiles.AddRectangleTileAtCell(0, 0, 0f, -4f, 16f, 8f);

        var wall    = tiles.GetTileAtCell(1, 0)!;
        var subRect = tiles.GetRectangleTilesAtCell(0, 0)[0];

        wall.RepositionDirections.ShouldBe(RepositionDirections.All);
        subRect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Left);
    }

    [Fact]
    public void AddRectangleTileAtCell_FlatRectBesideFullCell_SuppressesFullCellRightFace()
    {
        // Full-cell tile at (0,0): right face x=16, y in [0,16].
        // Left-half sub-cell rect at (1,0) center=(20,8), left face x=16, y in [0,16] → full coverage.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        tiles.AddRectangleTileAtCell(1, 0, -4f, 0f, 8f, 16f);

        var square  = tiles.GetTileAtCell(0, 0)!;
        var subRect = tiles.GetRectangleTilesAtCell(1, 0)[0];

        square.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Left);
        subRect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Right);
    }

    [Fact]
    public void AddRectangleTileAtCell_AdjacentPolygonWithFullEdgeCoverage_SuppressesRectFace()
    {
        // Slope polygon at cell (0,0) whose right edge runs the full cell height along x=16
        // (points: (-8,-8),(8,-8),(8,8) → right edge from (8,-8) to (8,8) in local → world x=16, y∈[0,16]).
        // Bottom-half sub-cell rect at cell (1,0): left face at x=16, y ∈ [0,8] — fully covered.
        // Rect's Left bit must be suppressed.
        var slope = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, slope);
        tiles.AddRectangleTileAtCell(1, 0, 0f, -4f, 16f, 8f);

        var rect = tiles.GetRectangleTilesAtCell(1, 0)[0];
        rect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Right);
    }

    [Fact]
    public void AddRectangleTileAtCell_AdjacentPolygonPartialEdgeCoverage_LeavesRectFaceLive()
    {
        // Polygon at cell (0,0) whose right edge only covers the BOTTOM half of the shared boundary:
        // points (-8,-8),(8,-8),(8,0),(-8,0) — right edge from (8,-8)→(8,0) → world x=16, y∈[0,8].
        // Sub-cell rect at (1,0) is TOP half: center (20, 12), y∈[8,16]. No overlap with edge's y∈[0,8].
        // Rect's Left must remain live.
        var poly = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  0f),
            new Vector2(-8f,  0f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, poly);
        tiles.AddRectangleTileAtCell(1, 0, 0f, 4f, 16f, 8f); // top-half rect in cell (1,0)

        var rect = tiles.GetRectangleTilesAtCell(1, 0)[0];
        rect.RepositionDirections.ShouldBe(RepositionDirections.All);
    }

    [Fact]
    public void AddRectangleTileAtCell_PolygonBelowRect_SuppressesRectDownFace()
    {
        // Polygon at cell (0,0) with a horizontal top edge along y=16 spanning the full cell width.
        // points (-8,-8),(8,-8),(8,8),(-8,8) — top edge (8,8)→(-8,8) → world y=16, x∈[0,16].
        // Top-half sub-cell rect at (0,1): center (8,20), bottom face y=16, x∈[0,16]. Fully covered.
        // Rect's Down must be suppressed.
        var poly = Polygon.FromPoints(new[]
        {
            new Vector2(-8f, -8f),
            new Vector2( 8f, -8f),
            new Vector2( 8f,  8f),
            new Vector2(-8f,  8f),
        });
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, poly);
        tiles.AddRectangleTileAtCell(0, 1, 0f, -4f, 16f, 8f); // bottom-half rect in cell (0,1)

        var rect = tiles.GetRectangleTilesAtCell(0, 1)[0];
        rect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Left | RepositionDirections.Right);
    }

    [Fact]
    public void AddRectangleTileAtCell_FullCoverageAddOrderReversed_SuppressesFullCellUpFace()
    {
        // Same geometry as FlatRectOnTopOfFullCell but sub-cell rect added first.
        // When the full-cell tile is added, adjacency pass must see the pre-existing sub-cell rect
        // and clear the full-cell's Up bit.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddRectangleTileAtCell(0, 1, 0f, -4f, 16f, 8f);
        tiles.AddTileAtCell(0, 0);

        var square  = tiles.GetTileAtCell(0, 0)!;
        var subRect = tiles.GetRectangleTilesAtCell(0, 1)[0];

        square.RepositionDirections.ShouldBe(
            RepositionDirections.Left | RepositionDirections.Right | RepositionDirections.Down);
        subRect.RepositionDirections.ShouldBe(
            RepositionDirections.Up | RepositionDirections.Left | RepositionDirections.Right);
    }

}
