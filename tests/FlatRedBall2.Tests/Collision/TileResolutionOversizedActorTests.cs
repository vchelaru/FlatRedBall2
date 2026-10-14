using System.Numerics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// OVERSIZED ACTOR BEHAVIOUR DIFFERS, PARITY UNKNOWN.
//
// Actors 2.5 tiles across (the 40x40 sq20/circ20) are pushed differently once plain tiles lose the center-side guard
// (per-tile RepositionHalfSize, #1472): the tiles they overlap by a sliver now push the whole distance too, so a
// corner tile can send a 40x40 actor sideways ~29 units on top of the push the other tiles give. Before the change
// every one of these cases stayed within half the actor plus half a tile of where it started, and mirrored layouts
// gave mirrored results.
//
// FlatRedBall 1 resolves tile by tile (each push moves the actor before the next tile is tested) where FRB2 sums the
// per-axis pushes of all overlapped tiles at once, so whether FRB1 would do the same is unknown. These tests only record
// what the engine does now and are excluded from the generic invariant scans (TileResolutionActors.IsOversizedParityUnknown).
public class TileResolutionOversizedActorTests
{
    private const float Tol = 1e-3f;

    // block3x3 occupies x 48..96, y 48..96. The actor starts below it with its center under the block's left half.
    [Fact]
    public void OversizedParityUnknown_Square40BelowABlock_IsPushedSidewaysAsWellAsDown()
    {
        var tiles = TileWorld.Map(TileResolutionMaps.Get("block3x3"));
        var spec = ActorSpec.Square(20f);

        var end = TileWorld.ResolveOnce(spec, tiles, 56.61f, 44.37f);

        // Was (56.61, 28): straight down.
        end.X.ShouldBe(28f, Tol);
        end.Y.ShouldBe(28f, Tol);
    }

    // Not mirror symmetric top to bottom: starts mirrored about the block's horizontal center line (y -> 144 - y) do not
    // give mirrored results (the block layout itself is symmetric).
    [Fact]
    public void OversizedParityUnknown_Square40AtTheBlocksLeftEdge_IsNotVerticallyMirrorSymmetric()
    {
        var tiles = TileWorld.Map(TileResolutionMaps.Get("block3x3"));
        var spec = ActorSpec.Square(20f);

        var lower = TileWorld.ResolveOnce(spec, tiles, 44.61f, 60.37f);
        var mirroredStart = TileWorld.ResolveOnce(spec, tiles, 44.61f, 144f - 60.37f);

        lower.X.ShouldBe(28f, Tol);
        lower.Y.ShouldBe(28f, Tol);
        // A symmetric engine would return (28, 116).
        mirroredStart.X.ShouldBe(28f, Tol);
        mirroredStart.Y.ShouldBe(28f, Tol);
    }

    [Fact]
    public void OversizedParityUnknown_Circle40BelowABlock_IsPushedAlongTheCornerArcAndDown()
    {
        var tiles = TileWorld.Map(TileResolutionMaps.Get("block3x3"));
        var spec = ActorSpec.Circle(20f);

        var end = TileWorld.ResolveOnce(spec, tiles, 56.61f, 44.37f);

        // Was (56.61, 28): straight down.
        end.X.ShouldBe(44.1708f, Tol);
        end.Y.ShouldBe(28f, Tol);
    }

    // A 40x40 actor in the open beside the staircase, pushed through it (its center crosses tiles). Before the change
    // it was pushed back out to the right without the surrounded pass (148, 76.37) and through the staircase with it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedParityUnknown_Square40BesideTheStaircase_IsThrownThroughIt(bool applyPass)
    {
        var tiles = TileWorld.Map(TileResolutionMaps.Get("stairs"));
        if (applyPass) tiles.AssignSurroundedTilesSolidOutward();
        var spec = ActorSpec.Square(20f);
        var start = new Vector2(130.61f, 76.37f);

        var end = TileWorld.ResolveOnce(spec, tiles, start.X, start.Y);

        end.X.ShouldBe(76f, Tol);
        end.Y.ShouldBe(28f, Tol);
        TileWorld.SegmentIsClear(tiles, start, end).ShouldBeFalse("the center crossed tiles");
    }

    // Beside the ledge the actor's center crosses the ledge's tiles. Before the change it ended at (76, 84) / (148, 84).
    [Theory]
    [InlineData(92.61f, 148f)]
    [InlineData(128.61f, 76f)]
    public void OversizedParityUnknown_Square40BesideTheLedge_IsCarriedAcrossIt(float startX, float endX)
    {
        var tiles = TileWorld.Map(TileResolutionMaps.Get("ledge"));
        var spec = ActorSpec.Square(20f);
        var start = new Vector2(startX, 64.37f);

        var passes = TileWorld.ResolvePasses(spec, tiles, start.X, start.Y, 3);

        passes[0].X.ShouldBe(endX, Tol);
        passes[0].Y.ShouldBe(116f, Tol);
        TileWorld.SegmentIsClear(tiles, start, passes[0]).ShouldBeFalse("the center crossed tiles");
    }
}
