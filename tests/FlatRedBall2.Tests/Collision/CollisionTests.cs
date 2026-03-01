using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class CollisionTests
{
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
        entityA.AddChild(rectA);

        var entityB = new Entity { X = 20f };
        var rectB = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityB.AddChild(rectB);

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
        entityA.AddChild(rectA);

        var entityB = new Entity();
        var rectB = new AxisAlignedRectangle { Width = 32f, Height = 32f };
        entityB.AddChild(rectB);

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
