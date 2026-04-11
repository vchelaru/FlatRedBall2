using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Math;

public class BoundsRectangleTests
{
    [Fact]
    public void EdgeProperties_CenteredAtOrigin_ReturnsCorrectEdges()
    {
        var rect = new BoundsRectangle(0f, 0f, 200f, 100f);

        rect.Left.ShouldBe(-100f);
        rect.Right.ShouldBe(100f);
        rect.Top.ShouldBe(50f);
        rect.Bottom.ShouldBe(-50f);
    }

    [Fact]
    public void EdgeProperties_OffCenter_ReturnsCorrectEdges()
    {
        var rect = new BoundsRectangle(50f, 30f, 200f, 100f);

        rect.Left.ShouldBe(-50f);
        rect.Right.ShouldBe(150f);
        rect.Top.ShouldBe(80f);
        rect.Bottom.ShouldBe(-20f);
    }

    [Fact]
    public void TwoParamConstructor_CentersAtOrigin()
    {
        var rect = new BoundsRectangle(400f, 300f);

        rect.CenterX.ShouldBe(0f);
        rect.CenterY.ShouldBe(0f);
        rect.Width.ShouldBe(400f);
        rect.Height.ShouldBe(300f);
    }
}
