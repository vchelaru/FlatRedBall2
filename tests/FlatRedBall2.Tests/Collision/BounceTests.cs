using System;
using FlatRedBall2.Collision;
using FlatRedBall2.Utilities;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class BounceTests
{
    // Ball: gravity pulls it down, launched with a random horizontal velocity.
    private class Ball : Entity
    {
        public Ball()
        {
            AccelerationY = -200f;
            AddChild(new Circle { Radius = 8f });
        }
    }

    private static Entity MakeWall(float x, float y, float width, float height)
    {
        var wall = new Entity { X = x, Y = y };
        wall.AddChild(new AxisAlignedRectangle { Width = width, Height = height });
        return wall;
    }

    private static FrameTime MakeFrame(float deltaSeconds)
        => new FrameTime(TimeSpan.FromSeconds(deltaSeconds), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void BounceOnCollision_BallHitsFloor_VelocityReversedWithElasticity()
    {
        float initialVelY = -100f;
        float elasticity = 0.9f;
        float expectedVelY = -initialVelY * elasticity; // 90f — upward after bounce

        var ball = new Ball();
        ball.Y = 0f;
        ball.VelocityY = initialVelY;

        // Floor just below ball: circle bottom is at -8, floor top at -12+8=-4, giving 4px overlap
        var floor = MakeWall(0f, -12f, 200f, 16f);

        var rel = new CollisionRelationship<Entity, Entity>(new[] { (Entity)ball }, new[] { floor });
        rel.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: elasticity);

        rel.RunCollisions();

        ball.VelocityY.ShouldBe(expectedVelY, tolerance: 0.01f);
        ball.CollidesWith(floor).ShouldBeFalse(); // separated
    }

    [Fact]
    public void BounceOnCollision_BallMovingDiagonally_OnlyNormalComponentReflected()
    {
        // Ball moving right AND downward hits a flat horizontal floor.
        // The floor normal is purely vertical, so only VelocityY should be reflected.
        // VelocityX must remain unchanged — it has no component along the collision normal.
        float velX = 150f;
        float velY = -300f;

        var ball = new Ball();
        ball.Y = 0f;
        ball.VelocityX = velX;
        ball.VelocityY = velY;

        var floor = MakeWall(0f, -12f, 200f, 16f);

        var rel = new CollisionRelationship<Entity, Entity>(new[] { (Entity)ball }, new[] { floor });
        rel.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 1f);

        rel.RunCollisions();

        ball.VelocityX.ShouldBe(velX, tolerance: 0.01f);   // X must be unchanged
        ball.VelocityY.ShouldBe(-velY, tolerance: 0.01f);  // Y must be reflected
    }

    [Fact]
    public void BounceOnCollision_TwoMovingEntities_BothVelocitiesAdjusted()
    {
        // Both entities moving toward each other — equal mass, perfect elasticity → velocities swap.
        var entityA = new Entity { X = 0f, VelocityX = 100f };
        entityA.AddChild(new Circle { Radius = 16f });

        var entityB = new Entity { X = 20f, VelocityX = -100f };
        entityB.AddChild(new Circle { Radius = 16f });

        // Distance = 20, combined radii = 32 → 12px overlap; they are colliding.
        entityA.CollidesWith(entityB).ShouldBeTrue();

        var rel = new CollisionRelationship<Entity, Entity>(new[] { entityA }, new[] { entityB });
        rel.BounceOnCollision(firstMass: 1f, secondMass: 1f, elasticity: 1f);
        rel.RunCollisions();

        // Perfect elastic collision with equal masses → velocities exchange.
        entityA.VelocityX.ShouldBe(-100f, tolerance: 0.01f);
        entityB.VelocityX.ShouldBe(100f, tolerance: 0.01f);
    }

    [Fact]
    public void BounceOnCollision_ShapeOnGrandchildEntity_UsesTopParentVelocity()
    {
        // Hierarchy: grandparent (VelX=100) → child entity P → Circle grandchild.
        // The grandparent is what moves; P and Circle have no independent velocity.
        // When grandparent collides with a wall, its velocity should be reversed.
        var grandparent = new Entity { X = 0f, VelocityX = 100f };
        var child = new Entity();
        grandparent.AddChild(child);
        child.AddChild(new Circle { Radius = 16f });

        // Wall at X=20, width=16 → left edge at 12; circle right edge at 16 → 4px overlap.
        var wall = new Entity { X = 20f };
        wall.AddChild(new AxisAlignedRectangle { Width = 16f, Height = 100f });

        grandparent.CollidesWith(wall).ShouldBeTrue();

        var rel = new CollisionRelationship<Entity, Entity>(new[] { grandparent }, new[] { wall });
        rel.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 1f);
        rel.RunCollisions();

        // Grandparent should now be moving left (velocity reversed).
        grandparent.VelocityX.ShouldBeLessThan(0f);
    }

    [Fact]
    public void Ball_WithGravityAndBounce_RemainsInsideArena()
    {
        // Seeded random for determinism
        var random = new GameRandom(seed: 42);
        float half = 200f;

        var ball = new Ball();
        ball.Y = random.Between(50f, 100f);       // spawn in upper half
        ball.VelocityX = random.Between(-150f, 150f);

        // Four walls forming a closed arena
        var floor   = MakeWall(  0f,  -half, 400f,  20f);
        var ceiling = MakeWall(  0f,   half, 400f,  20f);
        var left    = MakeWall(-half,    0f,  20f, 400f);
        var right   = MakeWall( half,    0f,  20f, 400f);

        var engine = new FlatRedBallService();
        var screen = new Screen();
        screen.Engine = engine;
        screen.Register(ball);
        screen.Register(floor);
        screen.Register(ceiling);
        screen.Register(left);
        screen.Register(right);

        screen.AddCollisionRelationship(new[] { (Entity)ball }, new[] { floor, ceiling, left, right })
              .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0.9f);

        var frame = MakeFrame(1f / 60f);
        for (int i = 0; i < 300; i++) // 5 seconds of simulation
            screen.Update(frame);

        ball.X.ShouldBeGreaterThan(-half);
        ball.X.ShouldBeLessThan(half);
        ball.Y.ShouldBeGreaterThan(-half);
        ball.Y.ShouldBeLessThan(half);
    }
}
