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
    public void Destroy_CallsCustomDestroy()
    {
        var entity = new DestroyTrackingEntity();

        entity.Destroy();

        entity.WasDestroyed.ShouldBeTrue();
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

    private class DestroyTrackingEntity : Entity
    {
        public bool WasDestroyed { get; private set; }
        public override void CustomDestroy() => WasDestroyed = true;
    }
}
