using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// One-way (cloud / jump-through) tile relationships layered on the normal tile resolution: the one-way gate
// only decides whether a separation applies, so seams, corners and speeds must still behave like solid tiles
// in the one direction that counts.
public class TileResolutionOneWayTests
{
    private const float Tol = 1e-3f;
    private const float Gravity = -900f;

    public static IEnumerable<object[]> Actors() =>
        new[] { "sq4", "sq8", "rect6x14", "circ4", "circ8", "poly8" }.Select(id => new object[] { id });

    // Cloud strip: tiles x 0..640 at y 32..48 (row 2); air everywhere else.
    private static TileShapes CloudStrip() =>
        TileWorld.Map(Enumerable.Repeat(new string('.', 40), 6).Concat(new[] { new string('#', 40) }).Concat(Enumerable.Repeat(new string('.', 40), 2)).ToArray());
    // 6 air rows on top (rows 8..3 -> y 48..144), strip at row 2 (y 32..48), 2 air rows below.

    private const float CloudTop = 48f, CloudBottom = 32f;

    private static TileSim CloudSim(string id, float x, float y, OneWayDirection direction)
    {
        var sim = new TileSim(TileResolutionActors.Get(id), CloudStrip(), x, y);
        sim.Relationship.OneWayDirection = direction;
        return sim;
    }

    // ── OneWayDirection.Up: jump-through floor ─────────────────────────────────

    [Theory]
    [MemberData(nameof(Actors))]
    public void Cloud_FallingOntoIt_LandsOnTopAtEveryX_IncludingSeams(string id)
    {
        var spec = TileResolutionActors.Get(id);
        for (float x = 5.3f; x < 635f; x += 7.7f)
        {
            var sim = CloudSim(id, x, CloudTop + 40f, OneWayDirection.Up);
            sim.Actor.AccelerationY = Gravity;

            sim.Step(60);

            sim.Actor.Y.ShouldBe(CloudTop + spec.HalfH, Tol, $"x={x}");
            sim.Actor.X.ShouldBe(x, Tol, $"x={x}");
        }
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void Cloud_WalkingAlongIt_AdvancesBySpeedEveryFrameAndStaysOnTop(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var sim = CloudSim(id, 20f, CloudTop + spec.HalfH, OneWayDirection.Up);
        sim.Actor.AccelerationY = Gravity;
        sim.Step(5);
        float previousX = sim.Actor.X;

        for (int frame = 0; frame < 250; frame++)
        {
            sim.Actor.VelocityX = 120f;
            sim.Step();

            (sim.Actor.X - previousX).ShouldBe(2f, 0.01f, $"frame {frame}");
            previousX = sim.Actor.X;
            sim.Actor.Y.ShouldBe(CloudTop + spec.HalfH, Tol, $"frame {frame}");
        }
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void Cloud_RisingThroughItFromBelow_PassesThroughAndLandsOnTop(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var sim = CloudSim(id, 100.3f, CloudBottom - spec.HalfH - 10f, OneWayDirection.Up);
        sim.Actor.AccelerationY = Gravity;
        sim.Actor.VelocityY = 420f; // peaks about 98 units up, well above the cloud top

        sim.Step(150);

        sim.Actor.Y.ShouldBe(CloudTop + spec.HalfH, Tol);
        sim.Actor.X.ShouldBe(100.3f, Tol);
    }

    // Moving sideways through the cloud's body (feet below its top) is never blocked or lifted.
    [Theory]
    [MemberData(nameof(Actors))]
    public void Cloud_MovingSidewaysThroughItsBody_IsNotBlockedOrLifted(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var sim = CloudSim(id, 100.3f, 40f, OneWayDirection.Up); // centered in the strip's height
        sim.Actor.VelocityX = 120f;

        for (int frame = 0; frame < 60; frame++)
        {
            sim.Step();
            sim.Actor.Y.ShouldBe(40f, Tol, $"frame {frame}");
        }

        sim.Actor.X.ShouldBe(100.3f + 120f, 0.05f);
    }

    // ── OneWayDirection.Down: ceiling-only barrier ────────────────────────────────

    [Theory]
    [MemberData(nameof(Actors))]
    public void CeilingBarrier_RisingIntoIt_IsStoppedUnderIt(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var sim = CloudSim(id, 100.3f, CloudBottom - spec.HalfH - 20f, OneWayDirection.Down);
        sim.Actor.VelocityY = 300f;

        sim.Step(60);

        sim.Actor.Y.ShouldBe(CloudBottom - spec.HalfH, Tol);
    }

    [Theory]
    [MemberData(nameof(Actors))]
    public void CeilingBarrier_FallingOntoIt_PassesThrough(string id)
    {
        var spec = TileResolutionActors.Get(id);
        var sim = CloudSim(id, 100.3f, CloudTop + spec.HalfH + 20f, OneWayDirection.Down);
        sim.Actor.VelocityY = -300f;

        sim.Step(60);

        sim.Actor.Y.ShouldBeLessThan(CloudBottom - spec.HalfH);
    }

    // ── Horizontal one-way doors ─────────────────────────────────────────────────

    // A wall that can be crossed left-to-right but not right-to-left (OneWayDirection.Right pushes the actor right).
    private static TileShapes DoorWall() => TileWorld.Map(Enumerable.Repeat("....#....", 10).ToArray()); // x 64..80

    [Fact]
    public void RightwardDoor_MovingLeftIntoItFromTheRight_IsStoppedAtItsRightFace()
    {
        var sim = new TileSim(ActorSpec.Square(4f), DoorWall(), 100f, 40.3f);
        sim.Relationship.OneWayDirection = OneWayDirection.Right;
        sim.Actor.VelocityX = -120f;

        sim.Step(60);

        sim.Actor.X.ShouldBe(80f + 4f, Tol);
    }

    [Fact]
    public void RightwardDoor_MovingRightIntoItFromTheLeft_PassesThrough()
    {
        var sim = new TileSim(ActorSpec.Square(4f), DoorWall(), 40f, 40.3f);
        sim.Relationship.OneWayDirection = OneWayDirection.Right;
        sim.Actor.VelocityX = 120f;

        sim.Step(60);

        sim.Actor.X.ShouldBeGreaterThan(84f);
    }

    [Fact]
    public void LeftwardDoor_MovingRightIntoItFromTheLeft_IsStoppedAtItsLeftFace()
    {
        var sim = new TileSim(ActorSpec.Square(4f), DoorWall(), 40f, 40.3f);
        sim.Relationship.OneWayDirection = OneWayDirection.Left;
        sim.Actor.VelocityX = 120f;

        sim.Step(60);

        sim.Actor.X.ShouldBe(64f - 4f, Tol);
    }

    [Fact]
    public void LeftwardDoor_MovingLeftIntoItFromTheRight_PassesThrough()
    {
        var sim = new TileSim(ActorSpec.Square(4f), DoorWall(), 100f, 40.3f);
        sim.Relationship.OneWayDirection = OneWayDirection.Left;
        sim.Actor.VelocityX = -120f;

        sim.Step(60);

        sim.Actor.X.ShouldBeLessThan(60f);
    }
}
