using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Frame-by-frame movement against tiles with the physics integrator and a bounce(0) relationship
// (the platformer setup): sliding along long surfaces without snagging, approaching a block from
// every direction, and travelling into thin and thick walls at several speeds.
public class TileResolutionMovementTests
{
    private const float Tol = 1e-3f;
    // dt = 1/60 is not exact in float, so per-frame travel accumulates ~1e-5 of error per frame.
    private const float Drift = 0.05f;
    private const float Gravity = -900f;

    public static IEnumerable<object[]> SlidingActors() =>
        new[] { "sq4", "sq8", "rect6x14", "rect14x6", "circ4", "circ8", "poly8", "diamond10" }.Select(id => new object[] { id });

    // ── Sliding along long flat surfaces: no snag at tile seams ────────────────────

    [Theory]
    [MemberData(nameof(SlidingActors))]
    public void Floor_WalkingRightAcrossManyTiles_AdvancesBySpeedEveryFrameAndStaysOnTheFloor(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(new string('.', 40), new string('#', 40)); // floor top y=16, 640 wide
        float speed = 120f; // 2 per frame
        float restY = 16f + spec.HalfH;
        var sim = new TileSim(spec, tiles, 20f, restY);
        sim.Actor.AccelerationY = Gravity;

        for (int frame = 1; frame <= 300; frame++)
        {
            sim.Actor.VelocityX = speed;
            sim.Step();

            sim.Actor.X.ShouldBe(20f + 2f * frame, Drift, $"frame {frame}");
            sim.Actor.Y.ShouldBe(restY, Tol, $"frame {frame}");
        }
    }

    [Theory]
    [MemberData(nameof(SlidingActors))]
    public void Floor_WalkingLeftAcrossManyTiles_AdvancesBySpeedEveryFrameAndStaysOnTheFloor(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(new string('.', 40), new string('#', 40));
        float restY = 16f + spec.HalfH;
        var sim = new TileSim(spec, tiles, 620f, restY);
        sim.Actor.AccelerationY = Gravity;

        for (int frame = 1; frame <= 300; frame++)
        {
            sim.Actor.VelocityX = -120f;
            sim.Step();

            sim.Actor.X.ShouldBe(620f - 2f * frame, Drift, $"frame {frame}");
            sim.Actor.Y.ShouldBe(restY, Tol, $"frame {frame}");
        }
    }

    // Walking over a floor whose tiles are separate columns of different depth is the same as a flat floor
    // as long as the tops line up.
    [Theory]
    [MemberData(nameof(SlidingActors))]
    public void ThickFloor_WalkingAcrossManyTiles_DoesNotSnagOnSeamsBetweenRows(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(new string('.', 40), new string('#', 40), new string('#', 40), new string('#', 40));
        float restY = 48f + spec.HalfH;
        var sim = new TileSim(spec, tiles, 20f, restY);
        sim.Actor.AccelerationY = Gravity;

        for (int frame = 1; frame <= 300; frame++)
        {
            sim.Actor.VelocityX = 120f;
            sim.Step();

            sim.Actor.X.ShouldBe(20f + 2f * frame, Drift, $"frame {frame}");
            sim.Actor.Y.ShouldBe(restY, Tol, $"frame {frame}");
        }
    }

    [Theory]
    [MemberData(nameof(SlidingActors))]
    public void Ceiling_MovingAlongManyTilesWhilePressingUp_AdvancesBySpeedEveryFrameAndStaysUnderIt(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(new string('#', 40), new string('.', 40)); // ceiling bottom y=16
        float restY = 16f - spec.HalfH;
        var sim = new TileSim(spec, tiles, 20f, restY);

        for (int frame = 1; frame <= 300; frame++)
        {
            sim.Actor.VelocityX = 120f;
            sim.Actor.VelocityY = 120f; // pressing into the ceiling, 2 per frame
            sim.Step();

            sim.Actor.X.ShouldBe(20f + 2f * frame, Drift, $"frame {frame}");
            sim.Actor.Y.ShouldBe(restY, Tol, $"frame {frame}");
        }
    }

    // Falling alongside a wall while pressing into it: no friction, the fall is identical to free fall.
    [Theory]
    [MemberData(nameof(SlidingActors))]
    public void Wall_FallingWhilePressingIntoIt_FallsExactlyAsFreeFallAndStaysAgainstTheWall(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(Enumerable.Repeat("#.", 60).ToArray()); // wall right face x=16, 960 tall
        float startY = 900f;
        var sim = new TileSim(spec, tiles, 16f + spec.HalfW, startY);
        sim.Actor.AccelerationY = Gravity;
        var free = new Entity { Y = startY, AccelerationY = Gravity };

        for (int frame = 1; frame <= 60; frame++)
        {
            sim.Actor.VelocityX = -90f; // pressing left into the wall
            sim.Step();
            free.PhysicsUpdate(new FrameTime(TimeSpan.FromSeconds(TileSim.Dt), TimeSpan.FromSeconds(TileSim.Dt), TimeSpan.Zero, TimeSpan.Zero));

            sim.Actor.X.ShouldBe(16f + spec.HalfW, Tol, $"frame {frame}");
            sim.Actor.Y.ShouldBe(free.Y, Tol, $"frame {frame}");
        }
    }

    [Theory]
    [MemberData(nameof(SlidingActors))]
    public void Wall_RisingWhilePressingIntoIt_RisesAtConstantSpeedAndStaysAgainstTheWall(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(Enumerable.Repeat(".#", 60).ToArray()); // wall left face x=16
        var sim = new TileSim(spec, tiles, 16f - spec.HalfW, 40f);

        for (int frame = 1; frame <= 120; frame++)
        {
            sim.Actor.VelocityX = 90f;
            sim.Actor.VelocityY = 180f; // 3 per frame
            sim.Step();

            sim.Actor.X.ShouldBe(16f - spec.HalfW, Tol, $"frame {frame}");
            sim.Actor.Y.ShouldBe(40f + 3f * frame, Drift, $"frame {frame}");
        }
    }

    // ── Approaching a lone block from every direction ───────────────────────────

    private static readonly string[] LoneBlock =
    {
        "...........",
        "...........",
        "...........",
        "...........",
        "...........",
        ".....#.....",
        "...........",
        "...........",
        "...........",
        "...........",
        "...........",
    }; // tile at cell (5,5): x 80..96, y 80..96, center (88, 88)

    public static IEnumerable<object[]> ApproachCases()
    {
        var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };
        foreach (var id in new[] { "sq4", "sq8", "circ8", "poly8", "rect6x14" })
            foreach (var (dx, dy) in directions)
                foreach (float speed in new[] { 60f, 180f, 300f }) // 1, 3, 5 per frame
                    yield return new object[] { id, dx, dy, speed };
    }

    // Aim the actor at points across the block's face (lateral offsets from -14 to +14) and require, every
    // frame: the actor's center never enters the tile, and once the run ends it is not overlapping the tile.
    [Theory]
    [MemberData(nameof(ApproachCases))]
    public void LoneBlock_ApproachingAtASlowSpeed_NeverEntersAndEndsOutside(string id, int dirX, int dirY, float speed)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(LoneBlock);
        var dir = Vector2.Normalize(new Vector2(dirX, dirY));
        var perp = new Vector2(-dir.Y, dir.X);
        var center = new Vector2(88f, 88f);

        for (float lateral = -14f; lateral <= 14f; lateral += 2f)
        {
            var aim = center + perp * lateral;
            var start = aim - dir * 70f;
            var sim = new TileSim(spec, tiles, start.X, start.Y);
            sim.Actor.VelocityX = dir.X * speed;
            sim.Actor.VelocityY = dir.Y * speed;

            for (int frame = 0; frame < 90; frame++)
            {
                sim.Step();
                var (c, r) = TileWorld.CellOf(sim.Actor.X, sim.Actor.Y);
                TileWorld.HasTile(tiles, c, r).ShouldBeFalse($"{id} dir=({dirX},{dirY}) v={speed} lateral={lateral} frame={frame}: center inside tile");
            }

            TileWorld.Penetrates(spec, sim.Actor.X, sim.Actor.Y, tiles)
                .ShouldBeFalse($"{id} dir=({dirX},{dirY}) v={speed} lateral={lateral}: still overlapping at the end");
        }
    }

    // ── Thin wall: several speeds, every phase of the approach ──────────────────

    public static IEnumerable<object[]> ThinWallSafeSpeeds()
    {
        foreach (var (id, half) in new[] { ("sq4", 4f), ("circ4", 4f), ("sq8", 8f), ("poly8", 8f) })
            foreach (float perFrame in new[] { 1f, 3f, 6f, 10f, half + 7.5f })
                yield return new object[] { id, perFrame };
    }

    // The wall is one tile thick: an actor moving less than half-actor + half-tile per frame can never have
    // its center jump past the wall's center line, whatever the sub-frame phase, so it is always held.
    [Theory]
    [MemberData(nameof(ThinWallSafeSpeeds))]
    public void ThinWall_ActorBelowTheTunnelingSpeed_IsAlwaysHeldAtTheNearFace_AtEveryPhase(string id, float perFrame)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(Enumerable.Repeat("....#....", 10).ToArray()); // wall x 64..80
        float heldX = 64f - spec.HalfW;

        for (float phase = 0f; phase < perFrame; phase += perFrame / 8f)
        {
            var sim = new TileSim(spec, tiles, 10f + phase, 40.3f, zeroVelocityIntoSurface: false);
            sim.Actor.VelocityX = perFrame * 60f;

            for (int frame = 0; frame < 120; frame++)
            {
                sim.Step();
                sim.Actor.X.ShouldBeLessThanOrEqualTo(heldX + Tol, $"{id} v={perFrame} phase={phase} frame={frame}");
            }
        }
    }

    // Past that speed some phases get the center over the wall's center line in a single frame; the wall then
    // pushes the actor out the far side.
    [Theory]
    [InlineData("sq4", 4f, 14f)]
    [InlineData("circ4", 4f, 14f)]
    [InlineData("sq8", 8f, 20f)]
    public void ThinWall_ActorAboveTheTunnelingSpeed_TunnelsAtSomePhases(string id, float half, float perFrame)
    {
        var spec = TileResolutionActors.Get(id);
        var tiles = TileWorld.Map(Enumerable.Repeat("....#....", 10).ToArray());
        int tunnelled = 0;

        for (float phase = 0f; phase < perFrame; phase += perFrame / 14f)
        {
            var sim = new TileSim(spec, tiles, 10f + phase, 40.3f, zeroVelocityIntoSurface: false);
            sim.Actor.VelocityX = perFrame * 60f;
            sim.Step(120);
            if (sim.Actor.X > 80f) tunnelled++;
        }

        tunnelled.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(3f)]
    [InlineData(8f)]
    [InlineData(11.5f)]
    public void ThickWall_ActorBelowTheTunnelingSpeed_IsAlwaysHeldAtTheNearFace_AtEveryPhase(float perFrame)
    {
        var spec = ActorSpec.Square(4f);
        var tiles = TileWorld.Map(Enumerable.Repeat("....###....", 10).ToArray()); // wall x 64..112

        for (float phase = 0f; phase < perFrame; phase += perFrame / 8f)
        {
            var sim = new TileSim(spec, tiles, 10f + phase, 40.3f, zeroVelocityIntoSurface: false);
            sim.Actor.VelocityX = perFrame * 60f;
            sim.Step(120);
            sim.Actor.X.ShouldBeLessThanOrEqualTo(60f + Tol, $"v={perFrame} phase={phase}");
        }
    }

    // Quirk: a 3-thick wall does not catch what a 1-thick wall lets through. Past the first tile's center line
    // the actor is not pushed, the middle tile has no faces, and the far tile pushes it out the back.
    [Fact]
    public void Quirk_ThickWall_ActorAboveTheTunnelingSpeed_EndsOnTheFarSideAtEveryPhase()
    {
        var spec = ActorSpec.Square(4f);
        var tiles = TileWorld.Map(Enumerable.Repeat("....###....", 10).ToArray());
        float perFrame = 14f;

        for (float phase = 0f; phase < perFrame; phase += perFrame / 14f)
        {
            var sim = new TileSim(spec, tiles, 10f + phase, 40.3f, zeroVelocityIntoSurface: false);
            sim.Actor.VelocityX = perFrame * 60f;
            sim.Step(120);
            sim.Actor.X.ShouldBeGreaterThan(112f, $"phase={phase}");
        }
    }

    // ── Falling onto a floor at increasing speeds ───────────────────────────────

    [Theory]
    [InlineData(1f)]
    [InlineData(4f)]
    [InlineData(8f)]
    [InlineData(15.5f)] // just under half-actor (8) + half-tile (8)
    public void ThinFloor_FallingBelowTheTunnelingSpeed_LandsOnTop(float perFrame)
    {
        var spec = ActorSpec.Square(8f);
        var tiles = TileWorld.Map(Enumerable.Repeat("..........", 6).Concat(new[] { "##########" }).Concat(Enumerable.Repeat("..........", 3)).ToArray()); // floor y 48..64
        var sim = new TileSim(spec, tiles, 80.3f, 120f);
        sim.Actor.VelocityY = -perFrame * 60f;

        sim.Step(200);

        sim.Actor.Y.ShouldBe(64f + 8f, Tol);
        sim.Actor.X.ShouldBe(80.3f, Tol);
    }

    // Quirk: a one-tile floor stops holding an actor that moves more than half-actor + half-tile in a frame.
    [Fact]
    public void Quirk_ThinFloor_FallingFasterThanHalfActorPlusHalfTile_FallsThrough()
    {
        var spec = ActorSpec.Square(8f);
        var tiles = TileWorld.Map(Enumerable.Repeat("..........", 6).Concat(new[] { "##########" }).Concat(Enumerable.Repeat("..........", 3)).ToArray());
        var sim = new TileSim(spec, tiles, 80.3f, 120f, zeroVelocityIntoSurface: false);
        float perFrame = 20f;
        sim.Actor.VelocityY = -perFrame * 60f;

        sim.Step(200);

        sim.Actor.Y.ShouldBeLessThan(48f, "ended below the floor");
    }

    // Quirk (FlatRedBall 1 would land the actor): a fast fall onto a 3-thick slab is not caught by the top row once
    // the center is past that row's center line, the middle row has no faces, and the bottom row pushes the actor
    // down and out of the slab.
    [Fact]
    public void Quirk_ThickSlab_FallingFasterThanHalfActorPlusHalfTile_PassesThroughTheWholeSlab()
    {
        var spec = ActorSpec.Square(8f);
        var tiles = TileWorld.Map(Enumerable.Repeat("..........", 8).Concat(new[] { "##########", "##########", "##########" }).Concat(Enumerable.Repeat("..........", 3)).ToArray()); // slab y 48..96
        var sim = new TileSim(spec, tiles, 80.3f, 200f, zeroVelocityIntoSurface: false);
        float perFrame = 20f;
        sim.Actor.VelocityY = -perFrame * 60f;

        sim.Step(200);

        sim.Actor.Y.ShouldBeLessThan(48f, "ended below the slab");
    }

    [Fact]
    public void ThickSlab_FallingBelowTheTunnelingSpeed_LandsOnTop()
    {
        var spec = ActorSpec.Square(8f);
        var tiles = TileWorld.Map(Enumerable.Repeat("..........", 8).Concat(new[] { "##########", "##########", "##########" }).Concat(Enumerable.Repeat("..........", 3)).ToArray());
        var sim = new TileSim(spec, tiles, 80.3f, 200f);
        sim.Actor.VelocityY = -12f * 60f;

        sim.Step(200);

        sim.Actor.Y.ShouldBe(96f + 8f, Tol);
    }
}
