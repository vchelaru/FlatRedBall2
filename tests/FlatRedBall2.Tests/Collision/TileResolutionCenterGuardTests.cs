using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// The "center-side guard": a tile with some SolidSides pushes an actor out of a face only when the actor's
// center is on the outward side of the tile's center line. Pins where that line sits, what it does to thin
// and thick walls, and the speeds at which a thin wall stops holding an actor. Where FlatRedBall 1 differs it is
// noted in the comment (FRB1 applies the half-size limit only to L-shaped corner tiles, and otherwise always pushes).
public class TileResolutionCenterGuardTests
{
    private const float Tol = 1e-3f;

    public static IEnumerable<object[]> ActorKinds() =>
        new[] { "sq4", "circ4" }.Select(id => new object[] { id }).Append(new object[] { "poly4" });

    private static ActorSpec Spec(string id) => id == "poly4" ? ActorSpec.PolyRect(4f, 4f) : TileResolutionActors.Get(id);

    // ── A face tile of a solid block: only one face exposed ─────────────────────

    private static readonly string[] Block3x3 = { "###", "###", "###" };

    // (face tile center, outward direction)
    public static IEnumerable<object[]> BlockFaces()
    {
        foreach (var id in new[] { "sq4", "circ4", "poly4" })
        {
            yield return new object[] { id, 24f, 40f, 0f, 1f };    // top-middle tile, Up face
            yield return new object[] { id, 24f, 8f, 0f, -1f };    // bottom-middle tile, Down face
            yield return new object[] { id, 8f, 24f, -1f, 0f };    // left-middle tile, Left face
            yield return new object[] { id, 40f, 24f, 1f, 0f };    // right-middle tile, Right face
        }
    }

    // Center just on the outward side of the tile's center line: pushed all the way out through the face.
    [Theory]
    [MemberData(nameof(BlockFaces))]
    public void FaceTile_ActorCenterJustOutsideTheCenterLine_IsPushedOutThroughTheFace(string id, float tileCx, float tileCy, float dirX, float dirY)
    {
        var tiles = TileWorld.Map(Block3x3);
        var spec = Spec(id);
        float half = 4f;

        var end = TileWorld.ResolveOnce(spec, tiles, tileCx + dirX * 0.5f, tileCy + dirY * 0.5f);

        end.X.ShouldBe(tileCx + dirX * (8f + half), dirX != 0f ? Tol : 0.5f + Tol);
        end.Y.ShouldBe(tileCy + dirY * (8f + half), dirY != 0f ? Tol : 0.5f + Tol);
    }

    // Center on the inner side of the center line: no push at all, the actor stays embedded in the block.
    [Theory]
    [MemberData(nameof(BlockFaces))]
    public void FaceTile_ActorCenterJustInsideTheCenterLine_IsNotPushed(string id, float tileCx, float tileCy, float dirX, float dirY)
    {
        var tiles = TileWorld.Map(Block3x3);
        var spec = Spec(id);
        var start = new Vector2(tileCx - dirX * 0.5f, tileCy - dirY * 0.5f);

        var end = TileWorld.ResolveOnce(spec, tiles, start.X, start.Y);

        end.ShouldBe(start);
    }

    // Exactly on the center line counts as the inner side.
    [Theory]
    [MemberData(nameof(BlockFaces))]
    public void FaceTile_ActorCenterExactlyOnTheCenterLine_IsNotPushed(string id, float tileCx, float tileCy, float dirX, float dirY)
    {
        var tiles = TileWorld.Map(Block3x3);
        var spec = Spec(id);
        var start = new Vector2(tileCx, tileCy);

        var end = TileWorld.ResolveOnce(spec, tiles, start.X, start.Y);

        end.ShouldBe(start);
    }

    // ── A corner tile of a block: two faces exposed, each with its own guard ─────

    // Top-left corner tile of the 3x3 block: x 0..16, y 32..48, center (8, 40), faces Left and Up.
    [Theory]
    [InlineData(9f, 41f, 9f, 52f)]    // upper-right quadrant: only Up is on the outward side -> up
    [InlineData(7f, 41f, 7f, 52f)]    // upper-left quadrant: Up and Left tie (11 each); Up is evaluated first
    [InlineData(7f, 39f, -4f, 39f)]   // lower-left quadrant: only Left -> left
    [InlineData(9f, 39f, 9f, 39f)]    // lower-right quadrant: neither -> not pushed
    public void CornerTile_PushDependsOnWhichQuadrantTheCenterIsIn(float startX, float startY, float endX, float endY)
    {
        var tiles = TileWorld.Map(Block3x3);
        var spec = ActorSpec.Square(4f);

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        end.X.ShouldBe(endX, Tol);
        end.Y.ShouldBe(endY, Tol);
    }

    // ── Thin one-tile floor: both faces exposed, the center line decides which way out ─

    [Theory]
    [MemberData(nameof(ActorKinds))]
    public void ThinFloor_ActorCenterAboveTheMidLine_IsPushedUpOntoTheFloor(string id)
    {
        var tiles = TileWorld.Map("........", "########"); // floor y 0..16
        var spec = Spec(id);

        var end = TileWorld.ResolveOnce(spec, tiles, 40.3f, 8.5f);

        end.X.ShouldBe(40.3f, Tol);
        end.Y.ShouldBe(16f + 4f, Tol);
    }

    // The flip side: once the center is below the mid line the floor pushes the actor DOWN, through it.
    [Theory]
    [MemberData(nameof(ActorKinds))]
    public void ThinFloor_ActorCenterBelowTheMidLine_IsPushedDownThroughTheFloor(string id)
    {
        var tiles = TileWorld.Map("........", "########");
        var spec = Spec(id);

        var end = TileWorld.ResolveOnce(spec, tiles, 40.3f, 7.5f);

        end.X.ShouldBe(40.3f, Tol);
        end.Y.ShouldBe(0f - 4f, Tol);
    }

    // ── Thick slab: the exposed rows push, the middle does nothing ─────────────

    private static readonly string[] ThreeThickSlab = { "........", "########", "########", "########" }; // y 0..48

    [Theory]
    [InlineData(41f, 52f)]   // center in the top row's upper half: pushed up and out
    [InlineData(39f, 39f)]   // center in the top row's lower half: Up refuses, nothing else pushes -> stays embedded
    [InlineData(24f, 24f)]   // middle row: no exposed face at all
    [InlineData(9f, 9f)]     // bottom row's upper half: Down refuses
    [InlineData(7f, -4f)]    // bottom row's lower half: pushed DOWN, out of the bottom of the slab
    public void ThickSlab_PushDependsOnWhichRowAndHalfTheCenterIsIn(float startY, float endY)
    {
        var tiles = TileWorld.Map(ThreeThickSlab);
        var spec = ActorSpec.Square(4f);

        var end = TileWorld.ResolveOnce(spec, tiles, 40.3f, startY);

        end.X.ShouldBe(40.3f, Tol);
        end.Y.ShouldBe(endY, Tol);
    }

    // ── One frame of travel into a thin wall: when does the wall stop holding? ─────
    //
    // A 1-tile wall's two exposed faces split at its center line. An actor whose leading edge starts 0.01
    // before the wall and moves v per frame ends up on its original side while v < half the actor + half a
    // tile (+ the 0.01 head start), and on the far side once its center passes the wall's center line.

    // Wall column at x 64..80 (center line 72); slab row at y 64..80 (center line 72).
    private static TileShapes VerticalWall() =>
        TileWorld.Map(Enumerable.Repeat("....#....", 10).ToArray());

    private static TileShapes HorizontalSlab() =>
        TileWorld.Map(Enumerable.Range(0, 10).Select(r => r == 5 ? "#########" : ".........").ToArray()); // row 4 -> y 64..80

    public static IEnumerable<object[]> ThinWallSpeeds()
    {
        foreach (var (id, half) in new[] { ("sq4", 4f), ("circ4", 4f), ("sq8", 8f), ("circ8", 8f), ("poly4", 4f) })
            foreach (var dir in new[] { "right", "left", "down", "up" })
            {
                yield return new object[] { id, half, dir, half + 7.9f, false };
                yield return new object[] { id, half, dir, half + 8.2f, true };
            }
    }

    [Theory]
    [MemberData(nameof(ThinWallSpeeds))]
    public void ThinWall_OneFrameOfTravel_HoldsTheActorUntilItsCenterPassesTheWallCenterLine(
        string id, float half, string direction, float distancePerFrame, bool expectTunnel)
    {
        var spec = Spec(id);
        bool vertical = direction is "down" or "up";
        var tiles = vertical ? HorizontalSlab() : VerticalWall();
        float sign = direction is "right" or "up" ? 1f : -1f;
        // Leading edge 0.01 short of the surface it is travelling toward.
        float centerAlong = sign > 0 ? 63.99f - half : 80.01f + half;
        float across = 40.3f;
        var sim = vertical
            ? new TileSim(spec, tiles, across, centerAlong)
            : new TileSim(spec, tiles, centerAlong, across);
        if (vertical) sim.Actor.VelocityY = sign * distancePerFrame * 60f;
        else sim.Actor.VelocityX = sign * distancePerFrame * 60f;

        sim.Step();

        float along = vertical ? sim.Actor.Y : sim.Actor.X;
        float heldSide = sign > 0 ? 64f - half : 80f + half;
        float farSide = sign > 0 ? 80f + half : 64f - half;
        along.ShouldBe(expectTunnel ? farSide : heldSide, Tol);
    }

    // ── Entering a thick wall ───────────────────────────────────────────────────

    // Quirk (FlatRedBall 1 would still push the actor back out here): in a 3-thick wall the first tile only
    // exposes its outer face, so once the center is past that tile's center line nothing pushes and the actor
    // simply sits inside the wall.
    [Fact]
    public void Quirk_ThickWall_ActorWhoseCenterPassesTheFirstTilesCenterLine_IsLeftInsideTheWall()
    {
        var tiles = TileWorld.Map(Enumerable.Repeat("....###....", 10).ToArray()); // wall x 64..112
        var spec = ActorSpec.Square(4f);

        var end = TileWorld.ResolveOnce(spec, tiles, 73f, 40.3f); // overlaps only the first tile (64..80), center right of 72

        end.ShouldBe(new Vector2(73f, 40.3f));
    }

    [Fact]
    public void ThickWall_ActorWhoseCenterIsStillBeforeTheFirstTilesCenterLine_IsPushedBackOut()
    {
        var tiles = TileWorld.Map(Enumerable.Repeat("....###....", 10).ToArray());
        var spec = ActorSpec.Square(4f);

        var end = TileWorld.ResolveOnce(spec, tiles, 71f, 40.3f);

        end.X.ShouldBe(60f, Tol);
        end.Y.ShouldBe(40.3f, Tol);
    }
}
