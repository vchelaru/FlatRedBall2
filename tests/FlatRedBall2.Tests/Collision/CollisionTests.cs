using System;
using System.Collections.Generic;
using FlatRedBall2.Collision;
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
}
