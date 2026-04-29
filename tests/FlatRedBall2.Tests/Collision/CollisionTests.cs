using System;
using System.Collections.Generic;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class CollisionTests
{
    [Fact]
    public void AllowDuplicatePairs_WhenFalse_EachPairFiresOnce()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f };
        var list = new[] { a, b };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(list, list);
        int fireCount = 0;
        rel.CollisionOccurred += (_, _) => fireCount++;

        rel.RunCollisions();

        fireCount.ShouldBe(1);
    }

    [Fact]
    public void AllowDuplicatePairs_WhenTrue_FiresEventForBothOrderings()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f };
        var list = new[] { a, b };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(list, list)
        {
            AllowDuplicatePairs = true,
        };
        var firedFirstArgs = new List<AxisAlignedRectangle>();
        rel.CollisionOccurred += (first, _) => firedFirstArgs.Add(first);

        rel.RunCollisions();

        firedFirstArgs.Count.ShouldBe(2);
        firedFirstArgs[0].ShouldBe(a); // first firing:  (a, b)
        firedFirstArgs[1].ShouldBe(b); // second firing: (b, a)
    }

    // ── One-way platform — tall body spanning two tiles ───────────────────────

    [Fact]
    public void OneWayPlatform_TallBodySpansLowerAndUpperTile_PushesUpNotThrough()
    {
        // When a tall entity body overlaps a lower one-way tile (landing from above) AND an
        // upper one-way tile (entered from below), GetSeparationFor must return a positive Y
        // so the one-way gate allows the collision. Previously the upper tile's larger downward
        // push overrode the lower tile's upward push, making sep.Y negative and causing the
        // one-way gate to reject the collision — the player fell through.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0); // bottom tile: top at y=16
        tiles.AddTileAtCell(0, 2); // upper tile: bottom at y=32 (one-cell gap)

        // Entity feet at y=14 (2 inside bottom tile), top at y=38 (6 inside upper tile).
        var entity = new Entity { X = 8f, Y = 14f };
        entity.LastPosition = new System.Numerics.Vector2(8f, 16f); // was on tile top last frame
        var shape = new AxisAlignedRectangle { Width = 12f, Height = 24f, Y = 12f };
        entity.Add(shape);

        var rel = new CollisionRelationship<Entity, TileShapeCollection>(
            new[] { entity }, new[] { tiles })
        {
            OneWayDirection = OneWayDirection.Up,
        };
        rel.BounceFirstOnCollision(elasticity: 0f);

        rel.RunCollisions();

        // Entity must be pushed up (grounded on bottom tile), not fall through.
        entity.LastReposition.Y.ShouldBeGreaterThan(0f);
    }

    [Fact]
    public void CollidesWith_AARectVsAARect_NotOverlapping_ReturnsFalse()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 100f, Y = 0f };

        a.CollidesWith(b).ShouldBeFalse();
    }

    [Fact]
    public void CollidesWith_AARectVsAARect_Overlapping_ReturnsTrue()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f, Y = 0f };

        a.CollidesWith(b).ShouldBeTrue();
    }

    [Fact]
    public void CollidesWith_AARectVsCircle_NotOverlapping_ReturnsFalse()
    {
        var rect = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var circle = new Circle { Radius = 10f, X = 100f, Y = 0f };

        rect.CollidesWith(circle).ShouldBeFalse();
    }

    [Fact]
    public void CollidesWith_AARectVsCircle_Overlapping_ReturnsTrue()
    {
        var rect = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var circle = new Circle { Radius = 20f, X = 28f, Y = 0f };

        rect.CollidesWith(circle).ShouldBeTrue();
    }

    [Fact]
    public void CollidesWith_CircleVsCircle_NotOverlapping_ReturnsFalse()
    {
        var a = new Circle { Radius = 16f, X = 0f, Y = 0f };
        var b = new Circle { Radius = 16f, X = 100f, Y = 0f };

        a.CollidesWith(b).ShouldBeFalse();
    }

    [Fact]
    public void CollidesWith_CircleVsCircle_Overlapping_ReturnsTrue()
    {
        var a = new Circle { Radius = 16f, X = 0f, Y = 0f };
        var b = new Circle { Radius = 16f, X = 20f, Y = 0f };

        a.CollidesWith(b).ShouldBeTrue();
    }

    [Fact]
    public void GetSeparationVector_AARectVsAARect_PointsAway()
    {
        // a is at 0, b is at 20 — b overlaps a's right side; MTV should push a left (negative X)
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f, Y = 0f };

        var sep = a.GetSeparationVector(b);

        sep.X.ShouldBeLessThan(0f);
        sep.Y.ShouldBe(0f);
    }

    [Fact]
    public void GetSeparationVector_RepositionDirection_ShouldRedirectReposition()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f, RepositionDirections = RepositionDirections.Down };
        var b = new Circle { Radius = 10, X = -25, Y = -10f };

        var sep = b.GetSeparationVector(a);

        sep.X.ShouldBe(0);
        sep.Y.ShouldBeLessThan(0, "because a's RepositionDirections should redirect the MTV downwards, even though the raw MTV from circle-vs-rect would be left");
    }

    // Circle at (0, -8): center inside the 32×32 rect at origin, near bottom face.
    // dx = 0 → targetCy = bottom − radius = −16 − 10 = −26; displacement = −26 − (−8) = −18.
    // After repositioning, the circle center is exactly 10 units (one radius) below the rect bottom.
    [Fact]
    public void GetSeparationVector_CircleCenterInsideRect_PushedDown_CenterIsRadiusBelowBottom()
    {
        var rect   = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f, RepositionDirections = RepositionDirections.Down };
        var circle = new Circle { Radius = 10f, X = 0f, Y = -8f };

        var sep = circle.GetSeparationVector(rect);

        sep.X.ShouldBe(0f);
        sep.Y.ShouldBe(-18f, tolerance: 0.001f, "circle center should land at rect.bottom − radius = −26");

        // Verify: after applying the separation the circle just grazes the bottom face.
        float newCy = circle.Y + sep.Y;                           // −26
        float distToBottom = MathF.Abs(newCy - (rect.Y - rect.Height / 2f)); // |−26 − (−16)| = 10
        distToBottom.ShouldBe(circle.Radius, tolerance: 0.001f);
    }

    // Circle at (−22, −18): outside both X and Y bounds → closest rect point is the bottom-left
    // corner (−16, −16). dx = 6 → targetCy = −16 − √(100−36) = −16 − 8 = −24; displacement = −6.
    // After repositioning, the circle edge just grazes the corner — no bounding-box overshoot.
    [Fact]
    public void GetSeparationVector_CircleNearCorner_PushedDown_EdgeGrazesCorner()
    {
        var rect   = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f, RepositionDirections = RepositionDirections.Down };
        var circle = new Circle { Radius = 10f, X = -22f, Y = -18f };

        var sep = circle.GetSeparationVector(rect);

        sep.X.ShouldBe(0f);
        sep.Y.ShouldBe(-6f, tolerance: 0.001f, "displacement should be exact — not the bounding-box overshoot");

        // Verify: after applying the separation the circle arc exactly touches the corner.
        float newCy = circle.Y + sep.Y;                           // −24
        float distToCorner = MathF.Sqrt(
            (circle.X - (rect.X - rect.Width  / 2f)) * (circle.X - (rect.X - rect.Width  / 2f)) +
            (newCy    - (rect.Y - rect.Height / 2f)) * (newCy    - (rect.Y - rect.Height / 2f)));
        distToCorner.ShouldBe(circle.Radius, tolerance: 0.001f);
    }

    [Fact]
    public void MoveBothOnCollision_BothObjectsMove()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b });
        rel.MoveBothOnCollision();

        rel.RunCollisions();

        a.X.ShouldBeLessThan(0f);
        b.X.ShouldBeGreaterThan(20f);
    }

    [Fact]
    public void MoveFirstOnCollision_MovesParentEntity()
    {
        // Verifies the parent entity's X changes, not the child shape's local X
        var entityA = new Entity { X = 0f };
        var rectA = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityA.Add(rectA);

        var entityB = new Entity { X = 20f };
        var rectB = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityB.Add(rectB);

        var rel = new CollisionRelationship<Entity, Entity>(new[] { entityA }, new[] { entityB });
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        entityA.X.ShouldBeLessThan(0f);    // parent moved
        rectA.X.ShouldBe(0f);              // child's local X unchanged
        entityA.CollidesWith(entityB).ShouldBeFalse();
    }

    [Fact]
    public void MoveFirstOnCollision_OnlyFirstMoves()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b });
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        b.X.ShouldBe(20f);                    // b is fixed
        a.CollidesWith(b).ShouldBeFalse();    // a resolved the overlap
    }

    [Fact]
    public void MoveSecondOnCollision_OnlySecondMoves()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b });
        rel.MoveSecondOnCollision();

        rel.RunCollisions();

        a.X.ShouldBe(0f);                     // a is fixed
        a.CollidesWith(b).ShouldBeFalse();    // b resolved the overlap
    }

    [Fact]
    public void OneWayDirection_Default_IsNone()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b });

        rel.OneWayDirection.ShouldBe(OneWayDirection.None);
    }

    [Fact]
    public void OneWayDown_AOverlapsBFromBelow_Separates()
    {
        // Ceiling-only barrier: A approaches B from below moving upward, gets pushed back down.
        // SAT picks Y axis (vertical-dominant overlap) and produces sep.Y < 0.
        var a = new OneWayTestPlayer { X = 0f, Y = -25f, VelocityY = 50f };
        a.LastPosition = new System.Numerics.Vector2(0f, -33f); // was below resolved Y last frame
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Down,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        a.Y.ShouldBeLessThan(-25f); // pushed down
        a.CollidesWith(b).ShouldBeFalse();
    }

    [Fact]
    public void OneWayDown_AOverlapsBFromAbove_DoesNotSeparate()
    {
        // A above B — sep.Y would be positive (push up). Down gate rejects.
        var a = new OneWayTestPlayer { X = 0f, Y = 25f };
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Down,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        a.Y.ShouldBe(25f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayDown_FromBelow_MovingDownward_DoesNotSeparate()
    {
        // Velocity gate: an entity below the ceiling but already falling shouldn't be pulled down further.
        var a = new OneWayTestPlayer { X = 0f, Y = -25f, VelocityY = -200f };
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Down,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        a.Y.ShouldBe(-25f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayDown_SeparationHasYComponent_NoXDisplacementApplied()
    {
        // Vertical-dominant overlap: SAT returns a pure-Y sep. X must not move (sep.X zeroed).
        var a = new OneWayTestPlayer { X = 5f, Y = -20f, VelocityY = 50f };
        a.LastPosition = new System.Numerics.Vector2(5f, -33f);
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Down,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        a.X.ShouldBe(5f);
        a.Y.ShouldBeLessThan(-20f);
    }

    [Fact]
    public void OneWayLeft_AOverlapsBFromLeft_Separates()
    {
        // One-way door blocking rightward motion. A approaches from the left moving right;
        // SAT picks X axis (horizontal-dominant overlap) with sep.X < 0 (push A back left).
        var a = new OneWayTestPlayer { X = -25f, Y = 0f, VelocityX = 50f };
        a.LastPosition = new System.Numerics.Vector2(-33f, 0f);
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Left,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        a.X.ShouldBeLessThan(-25f); // pushed left
        a.CollidesWith(b).ShouldBeFalse();
    }

    [Fact]
    public void OneWayLeft_AOverlapsBFromRight_DoesNotSeparate()
    {
        // A on the right side — sep.X would be positive (push right). Left gate rejects.
        var a = new OneWayTestPlayer { X = 25f, Y = 0f };
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Left,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        a.X.ShouldBe(25f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayLeft_FromLeft_MovingLeftward_DoesNotSeparate()
    {
        // Velocity gate: an entity already moving left shouldn't be pushed further left through the door.
        var a = new OneWayTestPlayer { X = -25f, Y = 0f, VelocityX = -200f };
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Left,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        a.X.ShouldBe(-25f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayLeft_SeparationHasXComponent_NoYDisplacementApplied()
    {
        var a = new OneWayTestPlayer { X = -20f, Y = 5f, VelocityX = 50f };
        a.LastPosition = new System.Numerics.Vector2(-33f, 5f);
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Left,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        a.Y.ShouldBe(5f);
        a.X.ShouldBeLessThan(-20f);
    }

    [Fact]
    public void OneWayRight_AOverlapsBFromRight_Separates()
    {
        // Mirror of Left: blocks leftward motion. A approaches from the right moving left.
        var a = new OneWayTestPlayer { X = 25f, Y = 0f, VelocityX = -50f };
        a.LastPosition = new System.Numerics.Vector2(33f, 0f);
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Right,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        a.X.ShouldBeGreaterThan(25f); // pushed right
        a.CollidesWith(b).ShouldBeFalse();
    }

    [Fact]
    public void OneWayRight_AOverlapsBFromLeft_DoesNotSeparate()
    {
        var a = new OneWayTestPlayer { X = -25f, Y = 0f };
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Right,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        a.X.ShouldBe(-25f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayRight_FromRight_MovingRightward_DoesNotSeparate()
    {
        var a = new OneWayTestPlayer { X = 25f, Y = 0f, VelocityX = 200f };
        a.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Right,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        a.X.ShouldBe(25f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayUp_AOverlapsBFromAbove_Separates()
    {
        // A sits just above B with a small vertical overlap. AabbVsAabb will produce sep.Y > 0
        // (push A upward), so the one-way Up gate fires.
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 25f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Up,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        a.Y.ShouldBeGreaterThan(25f); // pushed up
        a.CollidesWith(b).ShouldBeFalse();
    }

    [Fact]
    public void OneWayUp_AOverlapsBFromBelow_DoesNotSeparate()
    {
        // A sits below B with vertical overlap — sep.Y would be negative (push A down).
        // Gate rejects this: no separation, no event.
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = -25f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Up,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        a.Y.ShouldBe(-25f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayUp_AOverlapsBFromSide_DoesNotSeparate()
    {
        // Horizontal-dominant overlap: AabbVsAabb returns a pure-X separation (sep.Y == 0).
        // Gate rejects: sep.Y <= 0 means "not pushed upward".
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 25f, Y = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Up,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        a.X.ShouldBe(25f);
        a.Y.ShouldBe(0f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayUp_SeparationHasYComponent_NoXDisplacementApplied()
    {
        // A above B with vertical-dominant overlap — SAT returns a pure-Y sep, so X doesn't move.
        // This also pins the "sep.X zeroed before ApplyResponse" contract: any X in the sep would
        // translate into X motion here, and we'd see it.
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 5f, Y = 20f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Up,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        a.X.ShouldBe(5f);
        a.Y.ShouldBeGreaterThan(20f);
    }

    [Fact]
    public void AllowDropThrough_DefaultsToFalse()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(new[] { a }, new[] { b });

        rel.AllowDropThrough.ShouldBeFalse();
    }

    [Fact]
    public void OneWayUp_AllowDropThroughFalse_PlayerSuppressing_StillSeparates()
    {
        // Hard one-way barrier (e.g. Yoshi's Island ratchet door): AllowDropThrough = false.
        // Even with the player's drop-through flag active, separation must still fire.
        var player = MakeSuppressingPlayer(y: 25f);
        // Simulate "was on top of barrier last frame" so the positional gate accepts.
        player.LastPosition = new System.Numerics.Vector2(0f, 33f);
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { player }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Up,
            // AllowDropThrough = false (default)
        };
        rel.MoveFirstOnCollision();

        player.Platformer.IsSuppressingOneWayCollision.ShouldBeTrue();
        rel.RunCollisions();

        player.Y.ShouldBeGreaterThan(25f, "hard one-way barrier must push the player out regardless of drop-through state");
        player.CollidesWith(b).ShouldBeFalse();
    }

    [Fact]
    public void OneWayUp_AllowDropThroughTrue_PlayerSuppressing_SkipsSeparation()
    {
        // Cloud platform: AllowDropThrough = true. Player's drop-through flag bypasses the pair entirely.
        var player = MakeSuppressingPlayer(y: 25f);
        float startY = player.Y;
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { player }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Up,
            AllowDropThrough = true,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        player.Platformer.IsSuppressingOneWayCollision.ShouldBeTrue();
        rel.RunCollisions();

        player.Y.ShouldBe(startY, "drop-through active + AllowDropThrough=true → relationship bypassed");
        fired.ShouldBeFalse();
    }

    [Fact]
    public void Solid_TwoWay_DropThroughSuppression_DoesNotBypass()
    {
        // Drop-through is *direction-gated* inside TryApplyOneWayGate — it only has an effect
        // when OneWayDirection != None. A solid (two-way) relationship with
        // OneWayDirection = None must never be bypassed by the player's suppression flag, even
        // if AllowDropThrough is (deliberately / accidentally) set to true. Regression against
        // a hypothetical bug where AllowDropThrough became a global "skip collision" kill switch.
        var player = MakeSuppressingPlayer(y: 25f);
        float startY = player.Y;
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { player }, new[] { b })
        {
            OneWayDirection = OneWayDirection.None, // solid, two-way
            AllowDropThrough = true,                // intentionally misconfigured
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        player.Platformer.IsSuppressingOneWayCollision.ShouldBeTrue();
        rel.RunCollisions();

        player.Y.ShouldBeGreaterThan(startY, "solid floor must still push the player out");
        player.CollidesWith(b).ShouldBeFalse();
        fired.ShouldBeTrue();
    }

    [Fact]
    public void OneWayUp_NeverAboveTile_StartsFallingFromInside_DoesNotSeparate()
    {
        // Player jumped up into a tile from below, never cleared the top, and is now starting to
        // fall (VY < 0). The velocity gate alone allows this — but the player should still NOT
        // be popped onto the top. Requires a positional gate: lastPosition must have been at or
        // above the post-separation Y. Without LastPosition tracking, sep.Y > 0 + VY <= 0 isn't
        // enough.
        var player = new OneWayTestPlayer { Y = 12f, VelocityY = -50f };
        player.LastPosition = new System.Numerics.Vector2(0f, 14f); // last frame: also inside tile from below
        player.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { player }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Up,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        player.Y.ShouldBe(12f, "player never reached the top of the tile, must not be popped onto it");
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayUp_AOverlapsBFromAbove_MovingUpward_DoesNotSeparate()
    {
        // Player jumping up through a jump-through platform must not get popped onto its top.
        // Even when sep.Y > 0 (deep enough overlap that SAT picks the upward exit), an upward-
        // moving entity should pass through. Gate requires VelocityY <= 0 for OneWayDirection.Up.
        var player = new OneWayTestPlayer { Y = 25f, VelocityY = 200f };
        player.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var rel = new CollisionRelationship<OneWayTestPlayer, AxisAlignedRectangle>(new[] { player }, new[] { b })
        {
            OneWayDirection = OneWayDirection.Up,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        player.Y.ShouldBe(25f);
        fired.ShouldBeFalse();
    }

    [Fact]
    public void OneWayUp_PolygonSlope_UphillWalk_Separates()
    {
        // Regression: on a sloped one-way (cloud) tile, the surface Y at the player's current X
        // is higher than at LastPosition.X when walking uphill. The original flat gate would
        // reject every frame ("LastPos.Y < Position.Y + sep.Y") and the player would tunnel.
        // The slope-aware gate folds in the (lastSurface - thisSurface) delta so the check passes.
        var tiles = new TileShapeCollection { GridSize = 16f };
        // Right-ascending triangle in cell (0,0): world (0,0)→(16,0)→(16,16).
        tiles.AddPolygonTileAtCell(0, 0, Polygon.FromPoints(new[]
        {
            new System.Numerics.Vector2(-8f, -8f),
            new System.Numerics.Vector2( 8f, -8f),
            new System.Numerics.Vector2( 8f,  8f),
        }));

        var player = new OneWayTestPlayer
        {
            X = 8f, Y = 23f, VelocityY = 0f,
            LastPosition = new System.Numerics.Vector2(4f, 20f),
        };
        player.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });

        var rel = new CollisionRelationship<OneWayTestPlayer, TileShapeCollection>(
            new[] { player }, new[] { tiles })
        {
            OneWayDirection = OneWayDirection.Up,
            SlopeMode = SlopeCollisionMode.PlatformerFloor,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        player.Y.ShouldBeGreaterThan(23f, "uphill cloud slope must push the player up to the slope surface");
    }

    [Fact]
    public void OneWayUp_PolygonSlope_MovingUpward_DoesNotSeparate()
    {
        // Player rising into a sloped cloud from below — velocity gate still rejects regardless
        // of surface-Y delta. Ensures the slope-aware path didn't accidentally bypass the
        // VelocityY > 0 gate.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddPolygonTileAtCell(0, 0, Polygon.FromPoints(new[]
        {
            new System.Numerics.Vector2(-8f, -8f),
            new System.Numerics.Vector2( 8f, -8f),
            new System.Numerics.Vector2( 8f,  8f),
        }));

        var player = new OneWayTestPlayer { X = 8f, Y = 15f, VelocityY = 200f };
        player.Add(new AxisAlignedRectangle { Width = 32f, Height = 32f });

        var rel = new CollisionRelationship<OneWayTestPlayer, TileShapeCollection>(
            new[] { player }, new[] { tiles })
        {
            OneWayDirection = OneWayDirection.Up,
            SlopeMode = SlopeCollisionMode.PlatformerFloor,
        };
        rel.MoveFirstOnCollision();

        rel.RunCollisions();

        player.Y.ShouldBe(15f);
    }

    [Fact]
    public void OneWayUp_SlopeToAdjacentFlatTile_DoesNotSnapUp()
    {
        // Regression: player walks downhill off a slope onto an adjacent flat tile whose top
        // is higher than the slope surface at the transition point. Without the fix, the
        // surfaceDelta (lastSurface on slope − thisSurface on rect) massively loosens the
        // positional gate, allowing an 8px upward sep through the one-way gate.
        // Fix: surfaceDelta only uses polygon surfaces — rect tiles return null, delta = 0,
        // and the original flat gate correctly rejects.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0); // flat rect: center (8,8), spans [0,16]×[0,16], top = 16
        tiles.AddPolygonTileAtCell(1, 0, Polygon.FromPoints(new[]
        {
            new System.Numerics.Vector2(-8f, -8f),
            new System.Numerics.Vector2( 8f, -8f),
            new System.Numerics.Vector2( 8f,  8f),
        })); // slope: center (24,8), surface from (16,0) to (32,16)

        // Player center above rect center so SAT pushes UP (sep.Y > 0).
        // Shape 12×20: at (10, 18) spans X [4,16], Y [8,28]. Rect [0,16]×[0,16].
        // Y overlap = [8,16] = 8, X overlap = [4,16] = 12. Min = Y → sep = (0, 8).
        // LastPosition on the slope (X=20). Slope surface at X=20 = 4.
        // With rect fallback (bugged): thisSurface = rect top = 16, delta = 4−16 = −12.
        //   Gate: 16 >= 18+8+(−12)−ε = 13.999 → TRUE (bug: snaps up).
        // Without rect fallback (fixed): thisSurface = null, delta = 0.
        //   Gate: 16 >= 18+8+0−ε = 25.999 → FALSE (correctly rejects).
        var player = new OneWayTestPlayer
        {
            X = 10f, Y = 18f, VelocityY = -5f,
            LastPosition = new System.Numerics.Vector2(20f, 16f),
        };
        player.Add(new AxisAlignedRectangle { Width = 12f, Height = 20f });

        var rel = new CollisionRelationship<OneWayTestPlayer, TileShapeCollection>(
            new[] { player }, new[] { tiles })
        {
            OneWayDirection = OneWayDirection.Up,
            SlopeMode = SlopeCollisionMode.PlatformerFloor,
        };
        rel.MoveFirstOnCollision();
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        fired.ShouldBeFalse("slope-to-flat transition must not produce a large upward separation");
        player.Y.ShouldBe(18f);
    }

    // Minimal IPlatformerEntity used to exercise the drop-through gate from collision tests.
    // Collision shape attached so the rectangle side sees a real overlap.
    private sealed class OneWayTestPlayer : Entity, IPlatformerEntity
    {
        public PlatformerBehavior Behavior { get; } = new();
        public PlatformerBehavior Platformer => Behavior;
    }

    private sealed class DownHeldAxisInput : I2DInput
    {
        public float X => 0f;
        public float Y => -1f;
    }

    // Builds a player whose PlatformerBehavior is in the "airborne + Down held" state so
    // IsSuppressingOneWayCollision is true. Uses the real Update path — no reflection or
    // internal hooks — matching how the flag gets set at runtime.
    private static OneWayTestPlayer MakeSuppressingPlayer(float y)
    {
        var player = new OneWayTestPlayer { Y = y };
        var shape = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        player.Add(shape);

        player.Behavior.AirMovement = new PlatformerValues();
        player.Behavior.MovementInput = new DownHeldAxisInput();
        player.LastReposition = System.Numerics.Vector2.Zero; // airborne

        var frame = new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero);
        player.Behavior.Update(player, frame);
        return player;
    }

    [Fact]
    public void RunCollisions_SameList_EachPairCheckedExactlyOnce()
    {
        // Three overlapping rects — all 3 unordered pairs collide, each should fire exactly once.
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 10f };
        var c = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f };
        var list = new[] { a, b, c };
        var rel = new CollisionRelationship<AxisAlignedRectangle, AxisAlignedRectangle>(list, list);
        int fireCount = 0;
        rel.CollisionOccurred += (_, _) => fireCount++;

        rel.RunCollisions();

        fireCount.ShouldBe(3); // (a,b), (a,c), (b,c) — never (b,a), (c,a), or (c,b)
    }

    [Fact]
    public void SeparateFrom_EqualMasses_MovesHalfOverlap()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f };

        a.SeparateFrom(b, thisMass: 1f, otherMass: 1f);

        a.X.ShouldBe(-6f); // overlap was 12px; half assigned to a
    }

    [Fact]
    public void SeparateFrom_MovesEntityOutOfOverlap()
    {
        var entityA = new Entity();
        var rectA = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityA.Add(rectA);

        var entityB = new Entity();
        var rectB = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityB.Add(rectB);

        entityA.X = 0f;
        entityB.X = 20f;

        entityA.CollidesWith(entityB).ShouldBeTrue();

        entityA.SeparateFrom(entityB, thisMass: 0f, otherMass: 1f);

        entityA.CollidesWith(entityB).ShouldBeFalse();
    }

    [Fact]
    public void SeparateFrom_ZeroMass_MovesFullOverlap()
    {
        // mass=0 = massless: the full overlap is absorbed by this object
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f };

        a.SeparateFrom(b, thisMass: 0f, otherMass: 1f);

        a.X.ShouldBe(-12f); // overlap was 12px; all of it assigned to a
    }

    // ── WithFirstShape / WithSecondShape ──────────────────────────────────

    [Fact]
    public void WithFirstShape_OverlappingShape_DetectsCollision()
    {
        var entityA = new Entity { X = 0f };
        var rectA1 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f }; // at world X=0, overlaps entityB
        var rectA2 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 100f }; // at world X=100, no overlap
        entityA.Add(rectA1);
        entityA.Add(rectA2, isDefaultCollision: false);

        var entityB = new Entity { X = 20f };
        var rectB = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityB.Add(rectB);

        int fireCount = 0;
        var rel = new CollisionRelationship<Entity, Entity>(new[] { entityA }, new[] { entityB });
        rel.WithFirstShape(e => rectA1);
        rel.CollisionOccurred += (_, _) => fireCount++;
        rel.RunCollisions();

        fireCount.ShouldBe(1);
    }

    [Fact]
    public void WithFirstShape_NonOverlappingShape_NoCollision()
    {
        var entityA = new Entity { X = 0f };
        var rectA1 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };   // overlaps entityB
        var rectA2 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 100f };  // far away
        entityA.Add(rectA1, isDefaultCollision: false);
        entityA.Add(rectA2, isDefaultCollision: false);

        var entityB = new Entity { X = 20f };
        var rectB = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityB.Add(rectB);

        int fireCount = 0;
        var rel = new CollisionRelationship<Entity, Entity>(new[] { entityA }, new[] { entityB });
        rel.WithFirstShape(e => rectA2); // select the far shape — should not collide
        rel.CollisionOccurred += (_, _) => fireCount++;
        rel.RunCollisions();

        fireCount.ShouldBe(0);
    }

    [Fact]
    public void WithFirstShape_MoveFirstOnCollision_MovesEntityNotShapeOffset()
    {
        // Entity A's selected child rect overlaps entity B.
        // After separation, entity A's Position should shift; rectA's local offset should stay 0.
        var entityA = new Entity { X = 0f };
        var rectA = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };
        entityA.Add(rectA);

        var entityB = new Entity { X = 20f };
        var rectB = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityB.Add(rectB);

        var rel = new CollisionRelationship<Entity, Entity>(new[] { entityA }, new[] { entityB });
        rel.WithFirstShape(e => rectA).MoveFirstOnCollision();
        rel.RunCollisions();

        entityA.X.ShouldBeLessThan(0f, "entity position should have been pushed left");
        rectA.X.ShouldBe(0f, "shape's local offset should be unchanged — entity, not shape, was moved");
        entityA.CollidesWith(entityB).ShouldBeFalse("entities should no longer overlap after separation");
    }

    [Fact]
    public void WithSecondShape_OverlappingShape_DetectsCollision()
    {
        var entityA = new Entity { X = 0f };
        var rectA = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityA.Add(rectA);

        var entityB = new Entity { X = 20f };
        var rectB1 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };   // overlaps entityA
        var rectB2 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 200f }; // far away
        entityB.Add(rectB1);
        entityB.Add(rectB2, isDefaultCollision: false);

        int fireCount = 0;
        var rel = new CollisionRelationship<Entity, Entity>(new[] { entityA }, new[] { entityB });
        rel.WithSecondShape(e => rectB1);
        rel.CollisionOccurred += (_, _) => fireCount++;
        rel.RunCollisions();

        fireCount.ShouldBe(1);
    }

    [Fact]
    public void WithFirstShape_AndWithSecondShape_BothSelectorsApplied()
    {
        // Only the pair (rectA2, rectB2) overlaps. Using selectors for both sides should detect it.
        var entityA = new Entity { X = 0f };
        var rectA1 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f };   // world X=0
        var rectA2 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 200f };  // world X=200
        entityA.Add(rectA1, isDefaultCollision: false);
        entityA.Add(rectA2, isDefaultCollision: false);

        var entityB = new Entity { X = 0f };
        var rectB1 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 500f }; // world X=500, no overlap
        var rectB2 = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 200f }; // world X=200, overlaps rectA2
        entityB.Add(rectB1, isDefaultCollision: false);
        entityB.Add(rectB2, isDefaultCollision: false);

        int fireCount = 0;
        var rel = new CollisionRelationship<Entity, Entity>(new[] { entityA }, new[] { entityB });
        rel.WithFirstShape(_ => rectA2).WithSecondShape(_ => rectB2);
        rel.CollisionOccurred += (_, _) => fireCount++;
        rel.RunCollisions();

        fireCount.ShouldBe(1);
    }

    // ── Non-default shapes vs TileShapeCollection ────────────────────────
    // Models the "ledge probe" / "weak spot" / "muzzle point" pattern:
    // an auxiliary shape attached to an entity for manual queries or a
    // different collision relationship, which must NOT participate in the
    // entity's default collision against terrain.

    [Fact]
    public void NonDefaultShape_OverlappingTiles_DoesNotTriggerRelationship()
    {
        // Entity's default body sits above the tile row (Y=20, tile spans Y=0..16).
        // An auxiliary "foot probe" poked below the body overlaps the tile.
        // If the probe is non-default, the Entity-vs-TSC relationship must ignore it.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0); // tile occupying X=0..16, Y=0..16

        var entity = new Entity { X = 8f, Y = 20f };
        var body = new AxisAlignedRectangle { Width = 8f, Height = 8f }; // at (8,20), no tile overlap
        entity.Add(body);
        var footProbe = new AxisAlignedRectangle { Width = 2f, Height = 2f, Y = -13f }; // at (8,7), inside tile
        entity.Add(footProbe, isDefaultCollision: false);

        int fireCount = 0;
        var rel = new CollisionRelationship<Entity, TileShapeCollection>(
            new[] { entity }, new[] { tiles });
        rel.CollisionOccurred += (_, _) => fireCount++;
        rel.RunCollisions();

        fireCount.ShouldBe(0, "non-default probe must not drag the entity into a tile collision");
    }

    [Fact]
    public void NonDefaultShape_CanStillQueryTilesViaCollidesWith()
    {
        // Ledge-detection use case: a probe excluded from default collision
        // must still be usable for manual CollidesWith queries against a TSC.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        var entity = new Entity { X = 8f, Y = 20f };
        var footOnGround = new AxisAlignedRectangle { Width = 2f, Height = 2f, Y = -13f }; // at (8,7), in tile
        var footOffLedge = new AxisAlignedRectangle { Width = 2f, Height = 2f, X = 24f, Y = -13f }; // at (32,7), no tile
        entity.Add(footOnGround, isDefaultCollision: false);
        entity.Add(footOffLedge, isDefaultCollision: false);

        footOnGround.CollidesWith(tiles).ShouldBeTrue("probe over tile should report collision when queried directly");
        footOffLedge.CollidesWith(tiles).ShouldBeFalse("probe over empty space should report no collision");
    }

    [Fact]
    public void NonDefaultShape_DefaultBodyOverlappingTiles_StillTriggersRelationship()
    {
        // Inverse guard: excluding a probe must NOT suppress the default body's collision.
        var tiles = new TileShapeCollection { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        var entity = new Entity { X = 8f, Y = 8f }; // body sits inside the tile
        var body = new AxisAlignedRectangle { Width = 8f, Height = 8f };
        entity.Add(body);
        var probe = new AxisAlignedRectangle { Width = 2f, Height = 2f, X = 100f }; // far away
        entity.Add(probe, isDefaultCollision: false);

        int fireCount = 0;
        var rel = new CollisionRelationship<Entity, TileShapeCollection>(
            new[] { entity }, new[] { tiles });
        rel.CollisionOccurred += (_, _) => fireCount++;
        rel.RunCollisions();

        fireCount.ShouldBe(1, "default body overlapping a tile must still trigger the relationship");
    }
}
