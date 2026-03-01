using FlatRedBall2.Collision;
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
        parent.AddChild(child);

        child.AbsoluteX.ShouldBe(expectedAbsoluteX);
        child.AbsoluteY.ShouldBe(expectedAbsoluteY);
    }

    [Fact]
    public void AddChild_SetsParentOnChild()
    {
        var parent = new Entity();
        var child = new Entity();

        parent.AddChild(child);

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
        parent.AddChild(child);

        child.Destroy();

        parent.Children.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveChild_ClearsParentOnChild()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.AddChild(child);

        parent.RemoveChild(child);

        child.Parent.ShouldBeNull();
    }

    private class DestroyTrackingEntity : Entity
    {
        public bool WasDestroyed { get; private set; }
        public override void CustomDestroy() => WasDestroyed = true;
    }
}
