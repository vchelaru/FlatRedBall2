using FlatRedBall2.Collision;
using Shouldly;
using Xunit;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace FlatRedBall2.Tests.Collision;

/// <summary>
/// Shapes must come up looking the way FRB1/Glue draws them: opaque white, outline only.
/// A Glue project that never sets a color relies entirely on these defaults.
/// </summary>
public class ShapeVisualDefaultsTests
{
    [Fact]
    public void Color_DefaultsToOpaqueWhite()
    {
        var white = new XnaColor(255, 255, 255, 255);

        new Circle().Color.ShouldBe(white);
        new AARect().Color.ShouldBe(white);
        new Polygon().Color.ShouldBe(white);
        new TileShapes().Color.ShouldBe(white);
    }

    [Fact]
    public void IsFilled_DefaultsToFalse()
    {
        new Circle().IsFilled.ShouldBeFalse();
        new AARect().IsFilled.ShouldBeFalse();
        new Polygon().IsFilled.ShouldBeFalse();
        new TileShapes().IsFilled.ShouldBeFalse();
    }
}
