using System;
using System.Linq;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class EntityTests
{
    [Fact]
    public void AbsolutePosition_IncludesParentOffset()
    {
        float parentX = 100f, parentY = 50f;
        float childX = 10f, childY = 20f;
        float expectedAbsoluteX = parentX + childX;
        float expectedAbsoluteY = parentY + childY;

        var parent = new Entity { X = parentX, Y = parentY };
        var child = new Entity { X = childX, Y = childY };
        parent.Add(child);

        child.AbsoluteX.ShouldBe(expectedAbsoluteX);
        child.AbsoluteY.ShouldBe(expectedAbsoluteY);
    }

    [Fact]
    public void AbsoluteRotation_IncludesParentRotation()
    {
        var parentRotation = Angle.FromDegrees(45f);
        var childRotation = Angle.FromDegrees(30f);
        float expectedDegrees = 75f;

        var parent = new Entity { Rotation = parentRotation };
        var child = new Entity { Rotation = childRotation };
        parent.Add(child);

        child.AbsoluteRotation.Degrees.ShouldBe(expectedDegrees, tolerance: 0.001f);
    }

    [Fact]
    public void Add_SetsParentOnChild()
    {
        var parent = new Entity();
        var child = new Entity();

        parent.Add(child);

        child.Parent.ShouldBe(parent);
    }

    [Fact]
    public void Add_WithIsDefaultCollisionFalse_ShapeExcludedFromCollision()
    {
        var entity = new Entity { X = 0f };
        var circle = new Circle { Radius = 20f };
        entity.Add(circle, isDefaultCollision: false);

        var other = new Circle { Radius = 20f, X = 10f };

        entity.CollidesWith(other).ShouldBeFalse();
    }

    [Fact]
    public void Destroy_CallsCustomDestroy()
    {
        var entity = new DestroyTrackingEntity();

        entity.Destroy();

        entity.WasDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void Destroy_FiresDestroyedEvent()
    {
        var entity = new Entity();
        bool fired = false;
        entity.Destroyed += () => fired = true;

        entity.Destroy();

        fired.ShouldBeTrue();
    }

    [Fact]
    public void Destroy_DestroyedEvent_FiresAfterCustomDestroy()
    {
        var entity = new DestroyTrackingEntity();
        bool customDestroyRanFirst = false;
        entity.Destroyed += () => customDestroyRanFirst = entity.WasDestroyed;

        entity.Destroy();

        customDestroyRanFirst.ShouldBeTrue();
    }

    [Fact]
    public void Destroy_RemovesFromParentsChildren()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.Add(child);

        child.Destroy();

        parent.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Remove_ClearsParentOnChild()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.Add(child);

        parent.Remove(child);

        child.Parent.ShouldBeNull();
    }

    [Fact]
    public void SetDefaultCollision_CalledTwiceWithSameValue_IsIdempotent()
    {
        var entity = new Entity();
        var circle = new Circle { Radius = 20f };
        entity.Add(circle, isDefaultCollision: false);

        entity.SetDefaultCollision(circle, true);
        entity.SetDefaultCollision(circle, true); // second call — no duplicate

        entity.Shapes.Count(s => ReferenceEquals(s, circle)).ShouldBe(1);
    }

    [Fact]
    public void SetDefaultCollision_ShapeNotAChild_Throws()
    {
        var entity = new Entity();
        var circle = new Circle { Radius = 20f };
        // circle never Add()-ed to entity

        Should.Throw<InvalidOperationException>(() => entity.SetDefaultCollision(circle, false));
    }

    [Fact]
    public void SetDefaultCollision_False_ExcludesShapeFromCollision()
    {
        var entity = new Entity { X = 0f };
        var circle = new Circle { Radius = 20f };
        entity.Add(circle); // in default collision

        entity.SetDefaultCollision(circle, false);

        var other = new Circle { Radius = 20f, X = 10f };
        entity.CollidesWith(other).ShouldBeFalse();
    }

    [Fact]
    public void SetDefaultCollision_True_IncludesShapeInCollision()
    {
        var entity = new Entity { X = 0f };
        var circle = new Circle { Radius = 20f };
        entity.Add(circle, isDefaultCollision: false);

        entity.SetDefaultCollision(circle, true);

        var other = new Circle { Radius = 20f, X = 10f };
        entity.CollidesWith(other).ShouldBeTrue();
    }

    [Fact]
    public void IsAbsoluteVisible_DefaultsTrue()
    {
        var entity = new Entity();
        entity.IsAbsoluteVisible.ShouldBeTrue();
    }

    [Fact]
    public void IsAbsoluteVisible_FalseWhenSelfInvisible()
    {
        var entity = new Entity { IsVisible = false };
        entity.IsAbsoluteVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsAbsoluteVisible_FalseWhenParentInvisible()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.Add(child);

        parent.IsVisible = false;

        child.IsVisible.ShouldBeTrue();
        child.IsAbsoluteVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsAbsoluteVisible_RecoversWhenParentUnhidden_PreservingChildState()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.Add(child);

        parent.IsVisible = false;
        parent.IsVisible = true;

        // Child's own IsVisible was never touched, so it remains true and effective visibility returns.
        child.IsVisible.ShouldBeTrue();
        child.IsAbsoluteVisible.ShouldBeTrue();
    }

    [Fact]
    public void IsAbsoluteVisible_FalseWhenAncestorInvisible()
    {
        var grandparent = new Entity();
        var parent = new Entity();
        var child = new Entity();
        grandparent.Add(parent);
        parent.Add(child);

        grandparent.IsVisible = false;

        parent.IsVisible.ShouldBeTrue();
        child.IsVisible.ShouldBeTrue();
        child.IsAbsoluteVisible.ShouldBeFalse();
    }

    private class DestroyTrackingEntity : Entity
    {
        public bool WasDestroyed { get; private set; }
        public override void CustomDestroy() => WasDestroyed = true;
    }
}
