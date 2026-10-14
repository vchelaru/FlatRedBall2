using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Long flat surfaces made of many tiles. The seams between tiles must never snag: an actor
// sliding along a floor, wall or ceiling is pushed straight out by exactly its penetration.
public class TileResolutionFlatSurfaceTests
{
    private const float Tol = 1e-3f;
    private const float Pen = 3f;

    // Every actor shape/size that is centered over the surface. (Diamond is included: its tip is the contact point.)
    public static IEnumerable<object[]> Actors() =>
        new[] { "sq4", "sq8", "sq12", "rect6x14", "rect14x6", "circ4", "circ8", "circ12", "poly8", "diamond10" }
            .Select(id => new object[] { id });

    private static IEnumerable<float> Sweep(float from, float to, float step)
    {
        for (float v = from; v <= to; v += step) yield return v;
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void Floor_ActorEmbeddedFromAbove_IsPushedStraightUpByThePenetration_AtEveryX(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map("..........", "##########"); // top face at y = 16
        float floorTop = 16f;

        foreach (float x in Sweep(0.3f, 159.7f, 1.3f))
        {
            var end = TileWorld.ResolveOnce(spec, tiles, x, floorTop + spec.HalfH - Pen);

            end.X.ShouldBe(x, Tol, $"x={x}");
            end.Y.ShouldBe(floorTop + spec.HalfH, Tol, $"x={x}");
        }
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void Floor_ActorExactlyTouching_IsNotMoved_AtEveryX(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map("..........", "##########");
        float restY = 16f + spec.HalfH;

        foreach (float x in Sweep(0.3f, 159.7f, 1.3f))
        {
            var end = TileWorld.ResolveOnce(spec, tiles, x, restY);

            end.ShouldBe(new Vector2(x, restY), $"x={x}");
        }
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void Ceiling_ActorEmbeddedFromBelow_IsPushedStraightDownByThePenetration_AtEveryX(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map("##########", ".........."); // bottom face at y = 16
        float ceilingBottom = 16f;

        foreach (float x in Sweep(0.3f, 159.7f, 1.3f))
        {
            var end = TileWorld.ResolveOnce(spec, tiles, x, ceilingBottom - spec.HalfH + Pen);

            end.X.ShouldBe(x, Tol, $"x={x}");
            end.Y.ShouldBe(ceilingBottom - spec.HalfH, Tol, $"x={x}");
        }
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void WallOnItsLeft_ActorEmbedded_IsPushedStraightRightByThePenetration_AtEveryY(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map("#.", "#.", "#.", "#.", "#.", "#.", "#.", "#.", "#.", "#."); // right face at x = 16
        float wallRight = 16f;

        foreach (float y in Sweep(0.3f, 159.7f, 1.3f))
        {
            var end = TileWorld.ResolveOnce(spec, tiles, wallRight + spec.HalfW - Pen, y);

            end.X.ShouldBe(wallRight + spec.HalfW, Tol, $"y={y}");
            end.Y.ShouldBe(y, Tol, $"y={y}");
        }
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void WallOnItsRight_ActorEmbedded_IsPushedStraightLeftByThePenetration_AtEveryY(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(".#", ".#", ".#", ".#", ".#", ".#", ".#", ".#", ".#", ".#"); // left face at x = 16
        float wallLeft = 16f;

        foreach (float y in Sweep(0.3f, 159.7f, 1.3f))
        {
            var end = TileWorld.ResolveOnce(spec, tiles, wallLeft - spec.HalfW + Pen, y);

            end.X.ShouldBe(wallLeft - spec.HalfW, Tol, $"y={y}");
            end.Y.ShouldBe(y, Tol, $"y={y}");
        }
    }

    // A thick slab: the exposed faces behave like a thin surface; the interior tiles have no faces at all.
    [Theory]
    [MemberData(nameof(Actors))]
    public void ThickFloor_ActorEmbeddedFromAbove_IsPushedStraightUp_AtEveryX(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map("..........", "##########", "##########", "##########"); // top face at y = 48
        float floorTop = 48f;

        foreach (float x in Sweep(0.3f, 159.7f, 1.3f))
        {
            var end = TileWorld.ResolveOnce(spec, tiles, x, floorTop + spec.HalfH - Pen);

            end.X.ShouldBe(x, Tol, $"x={x}");
            end.Y.ShouldBe(floorTop + spec.HalfH, Tol, $"x={x}");
        }
    }

    // Floor tiles at the far end of a row are the ones that expose a side face as well.
    [Theory]
    [InlineData("sq4")]
    [InlineData("sq8")]
    [InlineData("sq12")]
    [InlineData("rect6x14")]
    public void Floor_ActorHangingOverTheEnd_IsStillPushedUp_WhenTheOverlapAcrossIsLargerThanThePenetration(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map("....", "####"); // floor spans x 0..64
        float floorTop = 16f;
        // Horizontal overlap with the end tile must exceed the 3-unit vertical penetration, or the shallower axis wins.
        float rightEndX = 64f + spec.HalfW - (Pen + 1f);
        float leftEndX = 0f - spec.HalfW + (Pen + 1f);

        var right = TileWorld.ResolveOnce(spec, tiles, rightEndX, floorTop + spec.HalfH - Pen);
        var left = TileWorld.ResolveOnce(spec, tiles, leftEndX, floorTop + spec.HalfH - Pen);

        right.ShouldBe(new Vector2(rightEndX, floorTop + spec.HalfH));
        left.ShouldBe(new Vector2(leftEndX, floorTop + spec.HalfH));
    }
}
