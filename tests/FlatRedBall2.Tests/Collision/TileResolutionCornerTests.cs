using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// An actor clipping the corner of a single isolated tile (SolidSides.All) is pushed out along the
// shallower overlap axis (rects/polygons) or along the corner normal (circles).
public class TileResolutionCornerTests
{
    private const float Tol = 1e-3f;
    private static readonly string[] Single =
    {
        ".....",
        ".....",
        "..#..",
        ".....",
        ".....",
    }; // tile spans x 32..48, y 32..48; center (40, 40)
    private const float TileCx = 40f, TileCy = 40f, Half = 8f;

    public static IEnumerable<object[]> Corners() =>
        new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) }.Select(c => new object[] { c.Item1, c.Item2 });

    // ── Rect actors ─────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Corners))]
    public void Rect_ClippingCorner_ShallowerOverlapOnX_IsPushedSidewaysByThatOverlap(int sx, int sy)
    {
        var tiles = TileWorld.Map(Single);
        float hw = 6f, hh = 6f, overlapX = 2f, overlapY = 5f;
        var spec = ActorSpec.Rect(hw, hh);

        var end = TileWorld.ResolveOnce(spec, tiles,
            TileCx + sx * (Half + hw - overlapX), TileCy + sy * (Half + hh - overlapY));

        end.X.ShouldBe(TileCx + sx * (Half + hw), Tol);
        end.Y.ShouldBe(TileCy + sy * (Half + hh - overlapY), Tol); // untouched
    }

    [Theory]
    [MemberData(nameof(Corners))]
    public void Rect_ClippingCorner_ShallowerOverlapOnY_IsPushedVerticallyByThatOverlap(int sx, int sy)
    {
        var tiles = TileWorld.Map(Single);
        float hw = 6f, hh = 6f, overlapX = 5f, overlapY = 2f;
        var spec = ActorSpec.Rect(hw, hh);

        var end = TileWorld.ResolveOnce(spec, tiles,
            TileCx + sx * (Half + hw - overlapX), TileCy + sy * (Half + hh - overlapY));

        end.X.ShouldBe(TileCx + sx * (Half + hw - overlapX), Tol); // untouched
        end.Y.ShouldBe(TileCy + sy * (Half + hh), Tol);
    }

    // Equal overlaps: the vertical push wins (AabbVsAabb only takes X when strictly shallower).
    [Theory]
    [MemberData(nameof(Corners))]
    public void Rect_ClippingCorner_EqualOverlaps_IsPushedVertically(int sx, int sy)
    {
        var tiles = TileWorld.Map(Single);
        float hw = 6f, hh = 6f, overlap = 3f;
        var spec = ActorSpec.Rect(hw, hh);

        var end = TileWorld.ResolveOnce(spec, tiles,
            TileCx + sx * (Half + hw - overlap), TileCy + sy * (Half + hh - overlap));

        end.X.ShouldBe(TileCx + sx * (Half + hw - overlap), Tol);
        end.Y.ShouldBe(TileCy + sy * (Half + hh), Tol);
    }

    // Center inside the tile's span on one axis: the push is straight out through the face it is
    // nearest to, even though the corner is closer overall.
    [Theory]
    [InlineData(0f, 5f, 0f, 1f)]   // above the top face, deep
    [InlineData(0f, -5f, 0f, -1f)] // below the bottom face
    [InlineData(5f, 0f, 1f, 0f)]   // beside the right face
    [InlineData(-5f, 0f, -1f, 0f)] // beside the left face
    public void Rect_CenteredOnAFace_IsPushedStraightOutOfThatFace(float offsetX, float offsetY, float dirX, float dirY)
    {
        var tiles = TileWorld.Map(Single);
        var spec = ActorSpec.Square(6f);
        // Start 5 units from the tile center along the face normal => center 3 units inside the face line.
        float startX = TileCx + (dirX != 0f ? dirX * 5f : offsetX);
        float startY = TileCy + (dirY != 0f ? dirY * 5f : offsetY);

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        end.X.ShouldBe(dirX != 0f ? TileCx + dirX * (Half + 6f) : startX, Tol);
        end.Y.ShouldBe(dirY != 0f ? TileCy + dirY * (Half + 6f) : startY, Tol);
    }

    // ── Polygon actors (axis-aligned rectangle polygons behave like AARects) ──────

    [Theory]
    [MemberData(nameof(Corners))]
    public void PolyRect_ClippingCorner_ShallowerOverlapOnX_IsPushedSideways(int sx, int sy)
    {
        var tiles = TileWorld.Map(Single);
        float hw = 6f, hh = 6f, overlapX = 2f, overlapY = 5f;
        var spec = ActorSpec.PolyRect(hw, hh);

        var end = TileWorld.ResolveOnce(spec, tiles,
            TileCx + sx * (Half + hw - overlapX), TileCy + sy * (Half + hh - overlapY));

        end.X.ShouldBe(TileCx + sx * (Half + hw), Tol);
        end.Y.ShouldBe(TileCy + sy * (Half + hh - overlapY), Tol);
    }

    [Theory]
    [MemberData(nameof(Corners))]
    public void PolyRect_ClippingCorner_ShallowerOverlapOnY_IsPushedVertically(int sx, int sy)
    {
        var tiles = TileWorld.Map(Single);
        float hw = 6f, hh = 6f, overlapX = 5f, overlapY = 2f;
        var spec = ActorSpec.PolyRect(hw, hh);

        var end = TileWorld.ResolveOnce(spec, tiles,
            TileCx + sx * (Half + hw - overlapX), TileCy + sy * (Half + hh - overlapY));

        end.X.ShouldBe(TileCx + sx * (Half + hw - overlapX), Tol);
        end.Y.ShouldBe(TileCy + sy * (Half + hh), Tol);
    }

    // ── Circle actors ─────────────────────────────────────────────────────────

    // Center in the diagonal region beyond the corner: pushed along the corner normal until it touches the corner.
    [Theory]
    [MemberData(nameof(Corners))]
    public void Circle_ClippingCorner_IsPushedAlongTheCornerNormalUntilTouchingTheCorner(int sx, int sy)
    {
        var tiles = TileWorld.Map(Single);
        float r = 8f;
        var corner = new Vector2(TileCx + sx * Half, TileCy + sy * Half);
        var start = corner + new Vector2(sx * 3f, sy * 4f); // 5 from the corner => overlaps by 3
        var spec = ActorSpec.Circle(r);

        var end = TileWorld.ResolveOnce(spec, tiles, start.X, start.Y);

        var expected = corner + Vector2.Normalize(new Vector2(sx * 3f, sy * 4f)) * r;
        end.X.ShouldBe(expected.X, Tol);
        end.Y.ShouldBe(expected.Y, Tol);
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(0f, -1f)]
    public void Circle_CenterOverAFace_IsPushedStraightOutByTheOverlap(float dirX, float dirY)
    {
        var tiles = TileWorld.Map(Single);
        float r = 8f;
        var spec = ActorSpec.Circle(r);
        float startX = TileCx + dirX * (Half + r - 3f) + dirY * 2f; // 2 units off-center along the face
        float startY = TileCy + dirY * (Half + r - 3f) + dirX * 2f;

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        end.X.ShouldBe(dirX != 0f ? TileCx + dirX * (Half + r) : startX, Tol);
        end.Y.ShouldBe(dirY != 0f ? TileCy + dirY * (Half + r) : startY, Tol);
    }

    // ── Diamond actors ────────────────────────────────────────────────────────

    // A diamond's vertex hits the tile top: pushed up by the vertex penetration.
    [Fact]
    public void Diamond_VertexInTopFace_IsPushedUpByThePenetration()
    {
        var tiles = TileWorld.Map(Single);
        var spec = ActorSpec.Diamond(10f);

        var end = TileWorld.ResolveOnce(spec, tiles, TileCx, TileCy + Half + 10f - 3f);

        end.X.ShouldBe(TileCx, Tol);
        end.Y.ShouldBe(TileCy + Half + 10f, Tol);
    }
}
