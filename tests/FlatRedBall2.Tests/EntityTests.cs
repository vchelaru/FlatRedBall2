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
    public void PauseMode_NewEntity_DefaultsToPausable()
    {
        var entity = new Entity();

        entity.PauseMode.ShouldBe(PauseMode.Pausable);
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

    [Fact]
    public void BroadPhaseRadius_SingleCircle_EqualsCircleRadius()
    {
        float expectedRadius = 16f;
        var entity = new Entity();
        entity.Add(new Circle { Radius = expectedRadius });

        entity.BroadPhaseRadius.ShouldBe(expectedRadius);
    }

    [Fact]
    public void BroadPhaseRadius_AfterRemovingShape_UpdatesToRemainingShape()
    {
        float smallRadius = 8f;
        float largeRadius = 32f;
        var entity = new Entity();
        var small = new Circle { Radius = smallRadius };
        var large = new Circle { Radius = largeRadius };
        entity.Add(small);
        entity.Add(large);

        entity.BroadPhaseRadius.ShouldBe(largeRadius);

        entity.Remove(large);

        entity.BroadPhaseRadius.ShouldBe(smallRadius);
    }

    [Fact]
    public void BroadPhaseRadius_AfterSetDefaultCollisionFalse_ExcludesShape()
    {
        float radius = 16f;
        var entity = new Entity();
        var circle = new Circle { Radius = radius };
        entity.Add(circle);

        entity.BroadPhaseRadius.ShouldBe(radius);

        entity.SetDefaultCollision(circle, false);

        entity.BroadPhaseRadius.ShouldBe(0f);
    }

    // #989 — BroadPhaseRadius goes stale when a shape resizes after Add.
    [Fact]
    public void BroadPhaseRadius_AfterCircleRadiusGrows_ReflectsNewRadius()
    {
        var entity = new Entity();
        var circle = new Circle { Radius = 8f };
        entity.Add(circle);
        entity.BroadPhaseRadius.ShouldBe(8f);

        circle.Radius = 64f;

        entity.BroadPhaseRadius.ShouldBe(64f);
    }

    [Fact]
    public void BroadPhaseRadius_AfterAARectWidthGrows_ReflectsNewRadius()
    {
        var entity = new Entity();
        var rect = new AARect { Width = 10f, Height = 10f };
        entity.Add(rect);
        entity.BroadPhaseRadius.ShouldBe(5f);

        rect.Width = 200f;

        entity.BroadPhaseRadius.ShouldBe(100f);
    }

    [Fact]
    public void BroadPhaseRadius_AfterPolygonSetPointsGrows_ReflectsNewRadius()
    {
        var entity = new Entity();
        var poly = Polygon.CreateRectangle(10f, 10f);
        entity.Add(poly);
        var smallRadius = entity.BroadPhaseRadius;

        poly.SetPoints(new System.Numerics.Vector2[]
        {
            new(-100f, -100f), new(100f, -100f), new(100f, 100f), new(-100f, 100f)
        });

        entity.BroadPhaseRadius.ShouldBeGreaterThan(smallRadius);
    }

    [Fact]
    public void BroadPhaseRadius_AfterLineEndPointGrows_ReflectsNewRadius()
    {
        var entity = new Entity();
        var line = new Line { EndPoint = new System.Numerics.Vector2(4f, 0f) };
        entity.Add(line);
        entity.BroadPhaseRadius.ShouldBe(4f);

        line.EndPoint = new System.Numerics.Vector2(100f, 0f);

        entity.BroadPhaseRadius.ShouldBe(100f);
    }

    [Fact]
    public void BroadPhaseRadius_AfterShapeLocalOffsetGrows_ReflectsNewOffset()
    {
        var entity = new Entity();
        var circle = new Circle { Radius = 4f, X = 0f };
        entity.Add(circle);
        entity.BroadPhaseRadius.ShouldBe(4f);

        circle.X = 50f;

        entity.BroadPhaseRadius.ShouldBe(54f);
    }

    // #989 — direct Parent reassignment bypasses Add()/Remove() shape-list bookkeeping
    // entirely (a pre-existing, documented limitation of manual Parent assignment — it does
    // not move the shape between _shapes lists). What must not happen is the *cache* going
    // stale: oldParent still nominally owns the shape, so once the shape's absolute position
    // jumps because its Parent changed, oldParent's cached BroadPhaseRadius must grow to
    // match — a stale-small cache here is the actual bug (broad-phase would wrongly cull a
    // real overlap).
    [Fact]
    public void BroadPhaseRadius_DirectParentReassignment_InvalidatesOldOwnersCache()
    {
        var oldParent = new Entity { X = 0f };
        var farParent = new Entity { X = 1000f };
        var circle = new Circle { Radius = 5f };
        oldParent.Add(circle);
        oldParent.BroadPhaseRadius.ShouldBe(5f);

        circle.Parent = farParent; // bypasses Remove()/Add() — oldParent._shapes still has circle

        oldParent.BroadPhaseRadius.ShouldBe(1005f);
    }

    // #989 — a nested Entity attached as another entity's shape must propagate growth up
    // through the Parent chain, since the outer entity's cache aggregates the inner one's.
    [Fact]
    public void BroadPhaseRadius_NestedEntityShapeGrows_PropagatesToGrandparent()
    {
        var outer = new Entity();
        var inner = new Entity();
        var circle = new Circle { Radius = 4f };
        inner.Add(circle);
        outer.Add(inner);
        var smallRadius = outer.BroadPhaseRadius;

        circle.Radius = 100f;

        outer.BroadPhaseRadius.ShouldBeGreaterThan(smallRadius);
    }

    private class DestroyTrackingEntity : Entity
    {
        public bool WasDestroyed { get; private set; }
        public override void CustomDestroy() => WasDestroyed = true;
    }
}
