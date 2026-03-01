using Xunit;
using FlatRedBall2.Collision;

namespace FlatRedBall2.Tests;

public class EntityTests
{
    [Fact]
    public void AddChild_SetsParentOnChild()
    {
        var parent = new Entity();
        var child = new Entity();

        parent.AddChild(child);

        Assert.Equal(parent, child.Parent);
    }

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

        Assert.Equal(expectedAbsoluteX, child.AbsoluteX);
        Assert.Equal(expectedAbsoluteY, child.AbsoluteY);
    }

    [Fact]
    public void RemoveChild_ClearsParentOnChild()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.AddChild(child);

        parent.RemoveChild(child);

        Assert.Null(child.Parent);
    }

    [Fact]
    public void Destroy_CallsCustomDestroy()
    {
        var entity = new DestroyTrackingEntity();

        entity.Destroy();

        Assert.True(entity.WasDestroyed);
    }

    [Fact]
    public void Destroy_RemovesFromParentsChildren()
    {
        var parent = new Entity();
        var child = new Entity();
        parent.AddChild(child);

        child.Destroy();

        Assert.Empty(parent.Children);
    }

    private class DestroyTrackingEntity : Entity
    {
        public bool WasDestroyed { get; private set; }
        public override void CustomDestroy() => WasDestroyed = true;
    }
}
