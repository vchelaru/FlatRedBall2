using System;
using System.Numerics;
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
            Add(new Circle { Radius = 8f });
        }
    }

    private static Entity MakeWall(float x, float y, float width, float height)
    {
        var wall = new Entity { X = x, Y = y };
        wall.Add(new AARect { Width = width, Height = height });
        return wall;
    }

    private static FrameTime MakeFrame(float deltaSeconds)
        => new FrameTime(TimeSpan.FromSeconds(deltaSeconds), TimeSpan.FromSeconds(deltaSeconds), TimeSpan.Zero, TimeSpan.Zero);

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
    public void BounceOnCollision_ShapeOnGrandchildEntity_UsesTopParentVelocity()
    {
        // Hierarchy: grandparent (VelX=100) → child entity P → Circle grandchild.
        // The grandparent is what moves; P and Circle have no independent velocity.
        // When grandparent collides with a wall, its velocity should be reversed.
        var grandparent = new Entity { X = 0f, VelocityX = 100f };
        var child = new Entity();
        grandparent.Add(child);
        child.Add(new Circle { Radius = 16f });

        // Wall at X=20, width=16 → left edge at 12; circle right edge at 16 → 4px overlap.
        var wall = new Entity { X = 20f };
        wall.Add(new AARect { Width = 16f, Height = 100f });

        grandparent.CollidesWith(wall).ShouldBeTrue();

        var rel = new CollisionRelationship<Entity, Entity>(new[] { grandparent }, new[] { wall });
        rel.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 1f);
        rel.RunCollisions();

        // Grandparent should now be moving left (velocity reversed).
        grandparent.VelocityX.ShouldBeLessThan(0f);
    }

    [Fact]
    public void BounceOnCollision_TwoMovingEntities_BothVelocitiesAdjusted()
    {
        // Both entities moving toward each other — equal mass, perfect elasticity → velocities swap.
        var entityA = new Entity { X = 0f, VelocityX = 100f };
        entityA.Add(new Circle { Radius = 16f });

        var entityB = new Entity { X = 20f, VelocityX = -100f };
        entityB.Add(new Circle { Radius = 16f });

        // Distance = 20, combined radii = 32 → 12px overlap; they are colliding.
        entityA.CollidesWith(entityB).ShouldBeTrue();

        var rel = new CollisionRelationship<Entity, Entity>(new[] { entityA }, new[] { entityB });
        rel.BounceOnCollision(firstMass: 1f, secondMass: 1f, elasticity: 1f);
        rel.RunCollisions();

        // Perfect elastic collision with equal masses → velocities exchange.
        entityA.VelocityX.ShouldBe(-100f, tolerance: 0.01f);
        entityB.VelocityX.ShouldBe(100f, tolerance: 0.01f);
    }

    // 45° up-right slope at cell (0,0): vertices (0,0), (16,0), (16,16); hypotenuse from
    // bottom-left to top-right. Outward normal of the hypotenuse (into the air side)
    // is (-1, 1)/√2 ≈ (-0.707, 0.707). Interior of the polygon is below-right of the line.
    private static Polygon UpRightSlope(float halfSize = 8f) => Polygon.FromPoints(new[]
    {
        new Vector2(-halfSize, -halfSize),
        new Vector2( halfSize, -halfSize),
        new Vector2( halfSize,  halfSize),
    });

    [Fact]
    public void BounceAgainstSlopePolygon_InStandardMode_ReflectsAlongSlopeNormal()
    {
        // A ball falling straight down onto a 45° up-right slope (no walls, no floor rects
        // in the collection — just the slope polygon) must reflect along the slope's
        // outward normal (-1/√2, 1/√2). For incoming V = (0, -100) and elasticity = 1:
        //   V·n = -100 · (1/√2) = -70.71
        //   V' = V - 2(V·n)n = (0, -100) + 141.42 · (-0.707, 0.707) ≈ (-100, 0)
        // So the ball should deflect horizontally to the LEFT, not bounce straight up.
        //
        // The wall-slam fix gates on "other is TileShapes && both axes non-zero"
        // and decomposes per-axis — which, for a slope polygon's diagonal SAT normal,
        // produces V' = (0, +100) (straight up) instead of the correct (-100, 0).
        // This test should be RED with that gate in place.
        var tiles = new TileShapes { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, UpRightSlope());

        var ball = new Entity { X = 9f, Y = 9.5f };
        var bodyCircle = new Circle { Radius = 4f };
        ball.Add(bodyCircle);
        ball.VelocityX = 0f;
        ball.VelocityY = -100f;

        // Sanity: make sure the setup actually penetrates the slope (SAT produces a non-zero diagonal sep).
        var sep = tiles.GetSeparationFor(bodyCircle, SlopeCollisionMode.Standard);
        sep.X.ShouldBeLessThan(-0.01f, "slope SAT must push ball left (away from the slope)");
        sep.Y.ShouldBeGreaterThan(0.01f, "slope SAT must push ball up (away from the slope)");

        var rel = new CollisionRelationship<Entity, TileShapes>(
            new[] { ball }, new[] { tiles });
        rel.SlopeMode = SlopeCollisionMode.Standard;
        rel.BounceFirstOnCollision(elasticity: 1f);

        rel.RunCollisions();

        // Correct reflection across (-1/√2, 1/√2): expect ball to go LEFT, not up.
        ball.VelocityX.ShouldBeLessThan(-50f,
            customMessage: "Ball should deflect LEFT off 45° slope (per-axis decomposition would leave X at 0)");
        ball.VelocityY.ShouldBeInRange(-10f, 10f,
            customMessage: "Ball's downward motion should be fully converted to horizontal, not reflected straight up");
    }

    [Fact]
    public void BounceAgainstTileCorner_DoesNotConvertHorizontalVelocityIntoUpwardKick()
    {
        // Reproduces the wall-slam pop-up: player running left into a wall while
        // gravity has sunk them 0.097 units into the floor.
        //
        // Geometry (GridSize = 16, collection origin at 0,0):
        //   Wall tile at (col=-1, row=0):  X [-16, 0], Y [0, 16]
        //   Floor tile at (col=0, row=-1): X [0, 16],  Y [-16, 0]
        //
        // Player AABB (16x32) centered at (-0.33, 15.903) — body spans Y [-0.097, 31.903]:
        //   X [-8.33, 7.67], Y [-0.097, 31.903]
        //   vs wall → min-axis exit right: sep = (8.33, 0)
        //   vs floor → min-axis exit up:   sep = (0, 0.097)
        // Aggregated sep → (8.33, 0.097) — identical to the live game repro.
        //
        // Physically, the player is pressed against two perpendicular surfaces
        // (wall on the left, floor below). Each should zero velocity along its own
        // axis → final velocity should be (0, 0).
        //
        // Pre-fix: AdjustVelocityFromSeparation normalizes the sum into one diagonal
        // normal ≈ (0.99993, 0.01167) and projects the -250 X velocity through it,
        // leaving ~(+0.1, -8.75) — wall push tilted ~2.92 units of horizontal
        // momentum upward, leaving Y velocity well short of the -11.67 it should
        // have been zeroed from.
        //
        // Post-fix: each axis is zeroed independently → (0, 0).
        var tiles = new TileShapes { GridSize = 16f };
        tiles.AddTileAtCell(-1, 0);
        tiles.AddTileAtCell(0, -1);

        var player = new Entity { X = -0.33342f, Y = 15.90277f };
        var body = new AARect { Width = 16f, Height = 32f };
        player.Add(body);
        player.VelocityX = -250f;
        player.VelocityY = -11.66669f;

        // Sanity-check the setup produces the same sep as the live game.
        var sep = tiles.GetSeparationFor(body, SlopeCollisionMode.PlatformerFloor);
        sep.X.ShouldBe(8.33342f, tolerance: 0.001f);
        sep.Y.ShouldBe(0.097229f, tolerance: 0.001f);

        var rel = new CollisionRelationship<Entity, TileShapes>(
            new[] { player }, new[] { tiles });
        rel.SlopeMode = SlopeCollisionMode.PlatformerFloor;
        rel.BounceFirstOnCollision(elasticity: 0f);

        rel.RunCollisions();

        // Both perpendicular contacts should zero their respective velocity
        // components — final velocity is (0, 0) to within rounding.
        player.VelocityX.ShouldBe(0f, tolerance: 0.01f,
            customMessage: "Wall contact should zero horizontal velocity");
        player.VelocityY.ShouldBe(0f, tolerance: 0.01f,
            customMessage: "Floor contact should zero vertical velocity (no diagonal-normal leakage from wall impulse)");
    }
}
