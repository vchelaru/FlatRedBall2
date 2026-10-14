using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Travelling into and around corners frame by frame: diagonal runs into inside corners, and sliding along a wall
// or floor past its free end.
public class TileResolutionCornerTravelTests
{
    private const float Tol = 1e-3f;
    private const float Drift = 0.05f;

    public static IEnumerable<object[]> Actors() =>
        new[] { "sq4", "sq8", "circ4", "circ8", "poly8", "rect6x14", "rect14x6" }.Select(id => new object[] { id });

    // ── Inside corners of a room ─────────────────────────────────────────────────

    // 7x5 room: interior x 16..96, y 16..64.
    private static readonly string[] Room = { "#######", "#.....#", "#.....#", "#.....#", "#######" };

    public static IEnumerable<object[]> RoomCornerRuns()
    {
        foreach (var id in new[] { "sq4", "sq8", "circ4", "circ8", "poly8", "rect6x14" })
            foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                foreach (float perFrame in new[] { 1f, 3f, 5f })
                    yield return new object[] { id, sx, sy, perFrame };
    }

    [Theory]
    [MemberData(nameof(RoomCornerRuns))]
    public void Room_DiagonalRunIntoAnInsideCorner_ComesToRestFlushInTheCorner(string id, int sx, int sy, float perFrame)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(Room);
        var sim = new TileSim(spec, tiles, 56f, 40f);
        sim.Actor.VelocityX = sx * perFrame * 60f;
        sim.Actor.VelocityY = sy * perFrame * 60f;

        for (int frame = 0; frame < 120; frame++)
        {
            sim.Step();
            TileWorld.Penetrates(spec, sim.Actor.X, sim.Actor.Y, tiles).ShouldBeFalse($"frame {frame}");
        }

        sim.Actor.X.ShouldBe(sx > 0 ? 96f - spec.HalfW : 16f + spec.HalfW, Tol);
        sim.Actor.Y.ShouldBe(sy > 0 ? 64f - spec.HalfH : 16f + spec.HalfH, Tol);
    }

    // ── Past the free end of a wall ──────────────────────────────────────────────

    // Wall on the left (x 0..16) from y=0 up to y=96. The actor rises at 3 per frame pressed against it. Passing the
    // wall's top corner must not slow it down or kick it sideways while it still overlaps the wall.
    [Theory]
    [MemberData(nameof(Actors))]
    public void Wall_RisingAlongItAndOffItsTopEnd_NeverStallsAtTheCorner(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var rows = Enumerable.Repeat("....", 8).Concat(Enumerable.Repeat("#...", 6)).ToArray(); // wall rows 0..5 => y 0..96
        var tiles = TileWorld.Map(rows);
        var sim = new TileSim(spec, tiles, 16f + spec.HalfW, 20f);
        float previousY = sim.Actor.Y;

        for (int frame = 0; frame < 80; frame++)
        {
            sim.Actor.VelocityX = -60f; // pressing into the wall
            sim.Actor.VelocityY = 180f; // 3 per frame
            sim.Step();

            (sim.Actor.Y - previousY).ShouldBe(3f, Drift, $"frame {frame}");
            previousY = sim.Actor.Y;
            TileWorld.Penetrates(spec, sim.Actor.X, sim.Actor.Y, tiles).ShouldBeFalse($"frame {frame}");
            // Flush against the face while the body still overlaps the wall's height. A circle rounds the top corner
            // once its center is above it, so it only has to stay flush while the center is below.
            float lowestOverlapReach = spec.Kind == ActorKind.Circle ? sim.Actor.Y : sim.Actor.Y - spec.HalfH;
            if (lowestOverlapReach < 96f)
                sim.Actor.X.ShouldBe(16f + spec.HalfW, Tol, $"frame {frame}");
        }
    }

    // Floor from x=0 to x=96; the actor walks right along it at 2 per frame and off its end.
    [Theory]
    [MemberData(nameof(Actors))]
    public void Floor_WalkingOffItsEnd_NeverStallsAtTheCorner(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map("..............", "######........"); // floor top y=16, x 0..96
        var sim = new TileSim(spec, tiles, 20f, 16f + spec.HalfH);
        sim.Actor.AccelerationY = -900f;
        sim.Step(5);
        float previousX = sim.Actor.X;

        for (int frame = 0; frame < 60; frame++)
        {
            sim.Actor.VelocityX = 120f;
            sim.Step();

            (sim.Actor.X - previousX).ShouldBe(2f, Drift, $"frame {frame}");
            previousX = sim.Actor.X;
        }

        sim.Actor.Y.ShouldBeLessThan(16f + spec.HalfH - 1f, "fell off the end");
    }

    // The same wall seen from the other side, travelling down it: mirror image of the rising case.
    [Theory]
    [MemberData(nameof(Actors))]
    public void Wall_FallingAlongItAndOffItsBottomEnd_NeverStallsAtTheCorner(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var rows = Enumerable.Repeat("#...", 6).Concat(Enumerable.Repeat("....", 8)).ToArray(); // wall rows 8..13 => y 128..224
        var tiles = TileWorld.Map(rows);
        var sim = new TileSim(spec, tiles, 16f + spec.HalfW, 220f);
        float previousY = sim.Actor.Y;

        for (int frame = 0; frame < 80; frame++)
        {
            sim.Actor.VelocityX = -60f;
            sim.Actor.VelocityY = -180f;
            sim.Step();

            (sim.Actor.Y - previousY).ShouldBe(-3f, Drift, $"frame {frame}");
            previousY = sim.Actor.Y;
            TileWorld.Penetrates(spec, sim.Actor.X, sim.Actor.Y, tiles).ShouldBeFalse($"frame {frame}");
        }
    }
}
