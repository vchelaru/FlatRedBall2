using Microsoft.Xna.Framework;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

public class ColorsTests
{
    [Theory]
    [InlineData(0f, 1f, 1f, 255, 0, 0)]      // red
    [InlineData(60f, 1f, 1f, 255, 255, 0)]   // yellow
    [InlineData(120f, 1f, 1f, 0, 255, 0)]    // green
    [InlineData(180f, 1f, 1f, 0, 255, 255)]  // cyan
    [InlineData(240f, 1f, 1f, 0, 0, 255)]    // blue
    [InlineData(300f, 1f, 1f, 255, 0, 255)]  // magenta
    public void FromHsv_PrimariesAndSecondaries_ReturnExpectedRgb(
        float h, float s, float v, int r, int g, int b)
    {
        var color = Colors.FromHsv(h, s, v);
        color.R.ShouldBe((byte)r);
        color.G.ShouldBe((byte)g);
        color.B.ShouldBe((byte)b);
        color.A.ShouldBe((byte)255);
    }

    [Fact]
    public void FromHsv_ZeroSaturation_IsGrayscale()
    {
        var color = Colors.FromHsv(123f, 0f, 0.5f);
        color.R.ShouldBe(color.G);
        color.G.ShouldBe(color.B);
        // 0.5 * 255 = 127.5 — accept either rounding.
        ((int)color.R).ShouldBeInRange(127, 128);
    }

    [Fact]
    public void FromHsv_ZeroValue_IsBlack()
    {
        var color = Colors.FromHsv(180f, 1f, 0f);
        color.R.ShouldBe((byte)0);
        color.G.ShouldBe((byte)0);
        color.B.ShouldBe((byte)0);
    }

    [Fact]
    public void FromHsv_HueWrapsAt360()
    {
        var atZero = Colors.FromHsv(0f, 1f, 1f);
        var atThreeSixty = Colors.FromHsv(360f, 1f, 1f);
        atThreeSixty.ShouldBe(atZero);
    }

    [Fact]
    public void FromHsv_NegativeHue_WrapsIntoRange()
    {
        // -60 should equal 300 (magenta).
        var negative = Colors.FromHsv(-60f, 1f, 1f);
        var positive = Colors.FromHsv(300f, 1f, 1f);
        negative.ShouldBe(positive);
    }

    [Fact]
    public void FromHsv_HalfValue_DimsProportionally()
    {
        var color = Colors.FromHsv(0f, 1f, 0.5f);
        ((int)color.R).ShouldBeInRange(127, 128);
        color.G.ShouldBe((byte)0);
        color.B.ShouldBe((byte)0);
    }
}
