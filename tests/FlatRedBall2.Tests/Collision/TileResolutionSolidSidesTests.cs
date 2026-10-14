using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Tiles whose SolidSides were set by hand (one-way style tiles, e.g. SolidSides.Up for a jump-through floor).
// Only the listed faces push, and each face only pushes an actor whose center is on its outward side of the
// tile's center line.
public class TileResolutionSolidSidesTests
{
    private const float Tol = 1e-3f;

    // A lone tile at cell (2,2): x 32..48, y 32..48, center (40, 40), with the given sides.
    private static TileShapes TileWithSides(SolidSides sides)
    {
        var tiles = TileWorld.Map(".....", ".....", "..#..", ".....", ".....");
        tiles.GetTileAtCell(2, 2)!.SolidSides = sides;
        return tiles;
    }

    public static IEnumerable<object[]> SingleSides() =>
        new[] { "sq4", "circ4", "poly8" }.SelectMany(id => new[]
        {
            new object[] { id, SolidSides.Up, 0f, 1f },
            new object[] { id, SolidSides.Down, 0f, -1f },
            new object[] { id, SolidSides.Left, -1f, 0f },
            new object[] { id, SolidSides.Right, 1f, 0f },
        });

    private static ActorSpec Spec(string id) => id == "poly8" ? ActorSpec.PolyRect(4f, 4f) : TileResolutionActors.Get(id);

    // Center on the outward side, pen 3 into the face: pushed out through that face by 3, no sideways movement.
    [Theory]
    [MemberData(nameof(SingleSides))]
    public void SingleSide_ActorOnTheOutwardSide_IsPushedOutThroughThatFace(string id, SolidSides side, float dirX, float dirY)
    {
        var tiles = TileWithSides(side);
        var spec = Spec(id);
        // 4 = actor half size; face is at 8 from the tile center along the direction.
        var start = new Vector2(40f + dirX * (8f + 4f - 3f), 40f + dirY * (8f + 4f - 3f));

        var end = TileWorld.ResolveOnce(spec, tiles, start.X, start.Y);

        end.X.ShouldBe(40f + dirX * 12f + (dirX == 0f ? 0f : 0f), Tol);
        end.Y.ShouldBe(40f + dirY * 12f, Tol);
    }

    // Plain tiles have no center-side guard (as in FlatRedBall 1): a tile whose only solid face is `side` pushes an
    // actor that overlaps it out through that face even when the actor is on the non-solid side. (One-way tiles use
    // OneWayDirection on the relationship instead, so they are unaffected.)
    [Theory]
    [MemberData(nameof(SingleSides))]
    public void SingleSide_ActorOnTheNonSolidSide_IsPushedThroughTheSolidFace(string id, SolidSides side, float dirX, float dirY)
    {
        var tiles = TileWithSides(side);
        var spec = Spec(id);
        var start = new Vector2(40f - dirX * (8f + 4f - 3f), 40f - dirY * (8f + 4f - 3f));

        var end = TileWorld.ResolveOnce(spec, tiles, start.X, start.Y);

        end.X.ShouldBe(dirX != 0f ? 40f + dirX * 12f : start.X, Tol);
        end.Y.ShouldBe(dirY != 0f ? 40f + dirY * 12f : start.Y, Tol);
    }

    // Deep into the tile with the center a unit past the center line: pushed out through the solid face.
    [Theory]
    [MemberData(nameof(SingleSides))]
    public void SingleSide_ActorCenterPastTheCenterLine_IsPushedOutThroughTheSolidFace(string id, SolidSides side, float dirX, float dirY)
    {
        var tiles = TileWithSides(side);
        var spec = Spec(id);
        var start = new Vector2(40f - dirX * 1f, 40f - dirY * 1f);

        var end = TileWorld.ResolveOnce(spec, tiles, start.X, start.Y);

        end.X.ShouldBe(dirX != 0f ? 40f + dirX * 12f : start.X, Tol);
        end.Y.ShouldBe(dirY != 0f ? 40f + dirY * 12f : start.Y, Tol);
    }

    // A one-way floor (Up only) approached from the side, center above the center line: it lifts the actor onto
    // it instead of blocking the side, because the side faces are not solid.
    [Fact]
    public void UpOnly_ActorOverlappingFromTheSideWithCenterAboveTheCenterLine_IsLiftedOntoTheTile()
    {
        var tiles = TileWithSides(SolidSides.Up);
        var spec = ActorSpec.Square(4f);

        var end = TileWorld.ResolveOnce(spec, tiles, 30f, 44f); // overlaps the left part of the tile by 2, 8 into the top

        end.X.ShouldBe(30f, Tol);
        end.Y.ShouldBe(48f + 4f, Tol);
    }

    [Fact]
    public void UpOnly_ActorOverlappingFromTheSideWithCenterBelowTheCenterLine_IsAlsoLiftedOntoTheTile()
    {
        var tiles = TileWithSides(SolidSides.Up);
        var spec = ActorSpec.Square(4f);

        var end = TileWorld.ResolveOnce(spec, tiles, 30f, 36f);

        end.X.ShouldBe(30f, Tol);
        end.Y.ShouldBe(52f, Tol);
    }

    // Two solid faces: the shallower of the two allowed axes is used.
    [Fact]
    public void UpAndRight_ActorInTheUpperRightQuadrant_IsPushedOutThroughTheShallowerFace()
    {
        var tiles = TileWithSides(SolidSides.Up | SolidSides.Right);
        var spec = ActorSpec.Square(4f);

        // Pen into top = 3, pen into right = 5 -> up.
        var up = TileWorld.ResolveOnce(spec, tiles, 48f - 5f + 4f + 0f, 48f + 4f - 3f);
        // Pen into top = 5, pen into right = 3 -> right.
        var right = TileWorld.ResolveOnce(spec, tiles, 48f + 4f - 3f, 48f + 4f - 5f);

        up.X.ShouldBe(47f, Tol);
        up.Y.ShouldBe(52f, Tol);
        right.X.ShouldBe(52f, Tol);
        right.Y.ShouldBe(47f, Tol);
    }

    // No faces at all: the tile is a ghost. (A fully surrounded interior tile looks like this.)
    [Fact]
    public void NoSides_Tile_NeverPushesAnything()
    {
        var tiles = TileWithSides(SolidSides.None);
        var spec = ActorSpec.Square(4f);

        foreach (var start in new[] { new Vector2(40f, 40f), new Vector2(48f, 44f), new Vector2(30f, 36f), new Vector2(44f, 52f) })
            TileWorld.ResolveOnce(spec, tiles, start.X, start.Y).ShouldBe(start);
    }

    // CollidesWith ignores SolidSides, so a ghost tile still reports an overlap.
    [Fact]
    public void NoSides_Tile_StillReportsCollidesWith()
    {
        var tiles = TileWithSides(SolidSides.None);
        var (actor, _) = TileWorld.Actor(ActorSpec.Square(4f), tiles, 40f, 40f);

        tiles.CollidesWith(actor).ShouldBeTrue();
    }

    // Editing a neighbor recomputes the sides of the tiles next to it, discarding hand edits there.
    [Fact]
    public void AddingANeighbor_RecomputesSides_DiscardingManualEdits()
    {
        var tiles = TileWithSides(SolidSides.Up);

        tiles.AddTileAtCell(3, 2); // right neighbor

        tiles.GetTileAtCell(2, 2)!.SolidSides.ShouldBe(SolidSides.Up | SolidSides.Down | SolidSides.Left);
    }
}
