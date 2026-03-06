using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class LineTests
{
    // ── AbsolutePoint helpers ─────────────────────────────────────────────

    [Fact]
    public void AbsolutePoint1_EqualsPosition()
    {
        var line = new Line { X = 10f, Y = 5f };

        line.AbsolutePoint1.ShouldBe(new Vector2(10f, 5f));
    }

    [Fact]
    public void AbsolutePoint2_IsPositionPlusEndPoint()
    {
        var line = new Line { X = 10f, Y = 5f, EndPoint = new Vector2(4f, -2f) };

        line.AbsolutePoint2.ShouldBe(new Vector2(14f, 3f));
    }

    // ── CollidesWith (ICollidable — pit of success) ───────────────────────

    [Fact]
    public void CollidesWith_LineVsLine_Crossing_ReturnsTrue()
    {
        // Horizontal: (−10,0)→(10,0); vertical: (0,−10)→(0,10).
        var h = new Line { X = -10f, Y = 0f,  EndPoint = new Vector2(20f, 0f) };
        var v = new Line { X = 0f,  Y = -10f, EndPoint = new Vector2(0f, 20f) };

        ((ICollidable)h).CollidesWith(v).ShouldBeTrue();
    }

    [Fact]
    public void CollidesWith_LineVsCircle_Intersecting_ReturnsTrue()
    {
        var line   = new Line   { X = -20f, Y = 0f, EndPoint = new Vector2(40f, 0f) };
        var circle = new Circle { X = 0f, Y = 5f, Radius = 10f };

        ((ICollidable)line).CollidesWith(circle).ShouldBeTrue();
    }

    [Fact]
    public void CollidesWith_LineVsAARect_Intersecting_ReturnsTrue()
    {
        var line = new Line                  { X = 0f, Y = 0f,   EndPoint = new Vector2(100f, 100f) };
        var rect = new AxisAlignedRectangle  { X = 0f, Y = 0f,   Width = 10f, Height = 10f };

        ((ICollidable)line).CollidesWith(rect).ShouldBeTrue();
    }

    // ── CollideAgainst(Line) ──────────────────────────────────────────────

    [Fact]
    public void CollideAgainst_LineVsLine_Crossing_ReturnsTrue()
    {
        var h = new Line { X = -10f, Y = 0f,  EndPoint = new Vector2(20f, 0f) };
        var v = new Line { X = 0f,  Y = -10f, EndPoint = new Vector2(0f, 20f) };

        h.CollideAgainst(v).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsLine_Parallel_ReturnsFalse()
    {
        var a = new Line { X = -10f, Y =  5f, EndPoint = new Vector2(20f, 0f) };
        var b = new Line { X = -10f, Y = -5f, EndPoint = new Vector2(20f, 0f) };

        a.CollideAgainst(b).ShouldBeFalse();
    }

    [Fact]
    public void CollideAgainst_LineVsLine_SharingEndpoint_ReturnsTrue()
    {
        // a ends at (10,0); b starts at (10,0).
        var a = new Line { X = 0f,  Y = 0f, EndPoint = new Vector2(10f, 0f) };
        var b = new Line { X = 10f, Y = 0f, EndPoint = new Vector2(0f, 10f) };

        a.CollideAgainst(b).ShouldBeTrue();
    }

    // ── CollideAgainst(Circle) ────────────────────────────────────────────

    [Fact]
    public void CollideAgainst_LineVsCircle_CircleCrossesSegment_ReturnsTrue()
    {
        // Horizontal segment at Y=0 from X=−20 to X=20; circle at (0,5) radius 10.
        var line   = new Line   { X = -20f, Y = 0f, EndPoint = new Vector2(40f, 0f) };
        var circle = new Circle { X = 0f, Y = 5f, Radius = 10f };

        line.CollideAgainst(circle).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsCircle_CircleFarAway_ReturnsFalse()
    {
        var line   = new Line   { X = -10f, Y = 0f, EndPoint = new Vector2(20f, 0f) };
        var circle = new Circle { X = 0f, Y = 50f, Radius = 5f };

        line.CollideAgainst(circle).ShouldBeFalse();
    }

    [Fact]
    public void CollideAgainst_LineVsCircle_CircleNearEndpointOnly_ReturnsTrue()
    {
        // Segment from (0,0) to (10,0); circle at (11,0) radius 2 — closest point is (10,0), dist = 1.
        var line   = new Line   { X = 0f, Y = 0f, EndPoint = new Vector2(10f, 0f) };
        var circle = new Circle { X = 11f, Y = 0f, Radius = 2f };

        line.CollideAgainst(circle).ShouldBeTrue();
    }

    // ── CollideAgainst(AxisAlignedRectangle) ─────────────────────────────

    [Fact]
    public void CollideAgainst_LineVsAARect_EndpointInsideRect_ReturnsTrue()
    {
        // First endpoint (0,0) is inside the 10×10 rect centered at origin.
        var line = new Line                 { X = 0f, Y = 0f, EndPoint = new Vector2(100f, 100f) };
        var rect = new AxisAlignedRectangle { X = 0f, Y = 0f, Width = 10f, Height = 10f };

        line.CollideAgainst(rect).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsAARect_SegmentCrossesRect_ReturnsTrue()
    {
        // Diagonal from (−20,−20) to (20,20) crosses a 10×10 rect at origin.
        var line = new Line                 { X = -20f, Y = -20f, EndPoint = new Vector2(40f, 40f) };
        var rect = new AxisAlignedRectangle { X = 0f,   Y = 0f,   Width = 10f, Height = 10f };

        line.CollideAgainst(rect).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsAARect_SegmentMissesRect_ReturnsFalse()
    {
        var line = new Line                 { X = -50f, Y = 0f, EndPoint = new Vector2(30f, 0f) };
        var rect = new AxisAlignedRectangle { X = 0f,   Y = 0f, Width = 10f, Height = 10f };

        line.CollideAgainst(rect).ShouldBeFalse();
    }

    // ── CollideAgainst(Polygon) ───────────────────────────────────────────

    [Fact]
    public void CollideAgainst_LineVsPolygon_SegmentCrossesEdge_ReturnsTrue()
    {
        // Square centered at (100, 0) — left face at x = 50. Ray from origin hits it.
        var line = new Line { X = 0f, Y = 0f, EndPoint = new Vector2(200f, 0f) };
        var poly = SquarePolygon(100f, 0f);

        line.CollideAgainst(poly).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsPolygon_SegmentMisses_ReturnsFalse()
    {
        var line = new Line { X = 0f, Y = 200f, EndPoint = new Vector2(200f, 0f) };
        var poly = SquarePolygon(100f, 0f);

        line.CollideAgainst(poly).ShouldBeFalse();
    }

    [Fact]
    public void CollideAgainst_LineVsPolygon_SegmentEndsBeforePolygon_ReturnsFalse()
    {
        // Polygon left face at x = 250; ray only reaches x = 200.
        var line = new Line { X = 0f, Y = 0f, EndPoint = new Vector2(200f, 0f) };
        var poly = SquarePolygon(300f, 0f);

        line.CollideAgainst(poly).ShouldBeFalse();
    }

    // ── Raycast closest-hit: Line vs list of Polygons ─────────────────────
    //
    // These replicate the SightLine.FindClosestHit pattern:
    // iterate all polygons, keep the hit closest to the ray origin.

    [Fact]
    public void Raycast_LineVsPolygonList_TwoAlongRay_ReturnsNearerHit()
    {
        // Near square left face at x = 100; far square left face at x = 300.
        // Ray from origin hits the near one first.
        var near = SquarePolygon(150f, 0f);
        var far  = SquarePolygon(350f, 0f);

        var hit = ClosestPolygonHit(new[] { far, near }, // shuffled on purpose
            new Vector2(0f, 0f), new Vector2(500f, 0f));

        hit.ShouldNotBeNull();
        hit!.Value.X.ShouldBe(100f, 0.01f);
        hit!.Value.Y.ShouldBe(0f,   0.01f);
    }

    [Fact]
    public void Raycast_LineVsPolygonList_OnlyOneHit_ReturnsThatHit()
    {
        var poly1 = SquarePolygon(150f,   0f); // on the ray
        var poly2 = SquarePolygon(350f, 300f); // far off to the side

        var hit = ClosestPolygonHit(new[] { poly1, poly2 },
            new Vector2(0f, 0f), new Vector2(500f, 0f));

        hit.ShouldNotBeNull();
        hit!.Value.X.ShouldBe(100f, 0.01f);
    }

    [Fact]
    public void Raycast_LineVsPolygonList_NoneHit_ReturnsNull()
    {
        var polygons = new[] { SquarePolygon(150f, 300f), SquarePolygon(350f, 300f) };

        ClosestPolygonHit(polygons, new Vector2(0f, 0f), new Vector2(500f, 0f))
            .ShouldBeNull();
    }

    [Fact]
    public void Raycast_LineVsPolygonList_ThreePolygons_ReturnsClosest()
    {
        var close  = SquarePolygon(100f, 0f); // left face at x = 50
        var middle = SquarePolygon(250f, 0f); // left face at x = 200
        var far    = SquarePolygon(400f, 0f); // left face at x = 350

        var hit = ClosestPolygonHit(new[] { far, middle, close },
            new Vector2(0f, 0f), new Vector2(500f, 0f));

        hit.ShouldNotBeNull();
        hit!.Value.X.ShouldBe(50f, 0.01f);
    }

    [Fact]
    public void Raycast_LineVsPolygonList_RotatedPolygon_HitsCorrectly()
    {
        // 100×20 plank rotated 90°: half-height (10) becomes the x-extent.
        // Centered at (200, 0) → left face at x = 190.
        var poly = Polygon.CreateRectangle(100f, 20f);
        poly.X = 200f;
        poly.Y = 0f;
        poly.Rotation = Angle.FromDegrees(90f);

        var hit = ClosestPolygonHit(new[] { poly },
            new Vector2(0f, 0f), new Vector2(400f, 0f));

        hit.ShouldNotBeNull();
        hit!.Value.X.ShouldBe(190f, 0.01f);
    }

    [Fact]
    public void Raycast_LineVsPolygonList_EmptyList_ReturnsNull()
    {
        ClosestPolygonHit(Array.Empty<Polygon>(),
            new Vector2(0f, 0f), new Vector2(500f, 0f))
            .ShouldBeNull();
    }

    // ── Raycast closest-hit: Line vs TileShapeCollection ─────────────────

    [Fact]
    public void Raycast_LineVsTiles_RayHitsTile_ReturnsHitPoint()
    {
        // Single tile at cell (5, 0) with GridSize=32 → tile left face at x = 160.
        var tiles = new TileShapeCollection { X = 0f, Y = 0f, GridSize = 32f };
        tiles.AddTileAtCell(5, 0);

        bool hit = tiles.Raycast(new Vector2(0f, 16f), new Vector2(400f, 16f),
            out Vector2 hitPoint, out _);

        hit.ShouldBeTrue();
        hitPoint.X.ShouldBe(160f, 0.01f);
    }

    [Fact]
    public void Raycast_LineVsTiles_RayMisses_ReturnsFalse()
    {
        var tiles = new TileShapeCollection { X = 0f, Y = 0f, GridSize = 32f };
        tiles.AddTileAtCell(5, 0);

        // Ray passes above all tiles.
        bool hit = tiles.Raycast(new Vector2(0f, 200f), new Vector2(400f, 200f),
            out _, out _);

        hit.ShouldBeFalse();
    }

    [Fact]
    public void Raycast_LineVsTiles_MultipleTilesAlongRay_ReturnsNearestTile()
    {
        var tiles = new TileShapeCollection { X = 0f, Y = 0f, GridSize = 32f };
        tiles.AddTileAtCell(3, 0); // left face at x = 96
        tiles.AddTileAtCell(7, 0); // left face at x = 224

        bool hit = tiles.Raycast(new Vector2(0f, 16f), new Vector2(400f, 16f),
            out Vector2 hitPoint, out _);

        hit.ShouldBeTrue();
        hitPoint.X.ShouldBe(96f, 0.01f);
    }

    [Fact]
    public void Raycast_LineVsTiles_RayEndsBeforeTile_ReturnsFalse()
    {
        var tiles = new TileShapeCollection { X = 0f, Y = 0f, GridSize = 32f };
        tiles.AddTileAtCell(10, 0); // left face at x = 320

        // Ray only reaches x = 200.
        bool hit = tiles.Raycast(new Vector2(0f, 16f), new Vector2(200f, 16f),
            out _, out _);

        hit.ShouldBeFalse();
    }

    [Fact]
    public void Raycast_LineVsTiles_NormalPointsTowardRayOrigin()
    {
        var tiles = new TileShapeCollection { X = 0f, Y = 0f, GridSize = 32f };
        tiles.AddTileAtCell(5, 0);

        tiles.Raycast(new Vector2(0f, 16f), new Vector2(400f, 16f),
            out _, out Vector2 normal);

        // Ray comes from the left, so the left face normal should point left (-X).
        normal.X.ShouldBeLessThan(0f);
        normal.Y.ShouldBe(0f, 0.01f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // Square polygon of side 100 centered at (cx, cy).
    private static Polygon SquarePolygon(float cx, float cy)
    {
        var poly = Polygon.FromPoints(new[]
        {
            new Vector2(-50f, -50f),
            new Vector2( 50f, -50f),
            new Vector2( 50f,  50f),
            new Vector2(-50f,  50f),
        });
        poly.X = cx;
        poly.Y = cy;
        return poly;
    }

    // Mirrors SightLine.FindClosestHit (polygon-only path).
    private static Vector2? ClosestPolygonHit(IEnumerable<Polygon> polygons, Vector2 start, Vector2 end)
    {
        Vector2? closest = null;
        float closestDistSq = float.MaxValue;
        foreach (var poly in polygons)
        {
            if (poly.Raycast(start, end, out Vector2 hit, out _))
            {
                float distSq = (hit - start).LengthSquared();
                if (distSq < closestDistSq) { closestDistSq = distSq; closest = hit; }
            }
        }
        return closest;
    }
}
