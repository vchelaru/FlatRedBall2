using System.Numerics;
using FlatRedBall2.Collision;
using Xunit;

namespace FlatRedBall2.Tests;

public class CollisionTests
{
    [Fact]
    public void AabbVsAabb_Overlapping_ReturnsTrue()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f, Y = 0f };

        Assert.True(a.CollidesWith(b));
    }

    [Fact]
    public void AabbVsAabb_NotOverlapping_ReturnsFalse()
    {
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 100f, Y = 0f };

        Assert.False(a.CollidesWith(b));
    }

    [Fact]
    public void AabbVsAabb_SeparationVector_PointsAway()
    {
        // a is at 0, b is at 20 — b overlaps a's right side; MTV should push a left (negative X)
        var a = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var b = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 20f, Y = 0f };

        var sep = a.GetSeparationVector(b);

        Assert.True(sep.X < 0f);
        Assert.Equal(0f, sep.Y);
    }

    [Fact]
    public void CircleVsCircle_Overlapping_ReturnsTrue()
    {
        var a = new Circle { Radius = 16f, X = 0f, Y = 0f };
        var b = new Circle { Radius = 16f, X = 20f, Y = 0f };

        Assert.True(a.CollidesWith(b));
    }

    [Fact]
    public void CircleVsCircle_NotOverlapping_ReturnsFalse()
    {
        var a = new Circle { Radius = 16f, X = 0f, Y = 0f };
        var b = new Circle { Radius = 16f, X = 100f, Y = 0f };

        Assert.False(a.CollidesWith(b));
    }

    [Fact]
    public void AabbVsCircle_Overlapping_ReturnsTrue()
    {
        var rect = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var circle = new Circle { Radius = 20f, X = 28f, Y = 0f };

        Assert.True(rect.CollidesWith(circle));
    }

    [Fact]
    public void AabbVsCircle_NotOverlapping_ReturnsFalse()
    {
        var rect = new AxisAlignedRectangle { Width = 32f, Height = 32f, X = 0f, Y = 0f };
        var circle = new Circle { Radius = 10f, X = 100f, Y = 0f };

        Assert.False(rect.CollidesWith(circle));
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

        Assert.True(entityA.CollidesWith(entityB));

        entityA.SeparateFrom(entityB, thisMass: 1f, otherMass: 0f);

        Assert.False(entityA.CollidesWith(entityB));
    }
}
