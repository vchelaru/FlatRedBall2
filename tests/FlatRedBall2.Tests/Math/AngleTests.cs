using System;
using System.Numerics;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Math;

public class AngleTests
{
    [Fact]
    public void Add_SumsRadians()
    {
        float expectedRadians = MathF.PI;

        var result = Angle.FromRadians(MathF.PI / 2f) + Angle.FromRadians(MathF.PI / 2f);

        result.Radians.ShouldBe(expectedRadians, tolerance: 0.00001f);
    }

    [Fact]
    public void Equality_SameRadiansAreEqual()
    {
        var a = Angle.FromDegrees(45f);
        var b = Angle.FromDegrees(45f);

        b.ShouldBe(a);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void FromDegrees_StoresCorrectRadians()
    {
        float expectedRadians = MathF.PI;

        var angle = Angle.FromDegrees(180f);

        angle.Radians.ShouldBe(expectedRadians, tolerance: 0.00001f);
    }

    [Fact]
    public void FromRadians_StoresCorrectDegrees()
    {
        float expectedDegrees = 90f;

        var angle = Angle.FromRadians(MathF.PI / 2f);

        angle.Degrees.ShouldBe(expectedDegrees, tolerance: 0.0001f);
    }

    [Fact]
    public void Lerp_MidpointIsHalfway()
    {
        var a = Angle.FromDegrees(0f);
        var b = Angle.FromDegrees(90f);
        float expectedDegrees = 45f;

        var result = Angle.Lerp(a, b, 0.5f);

        result.Degrees.ShouldBe(expectedDegrees, tolerance: 0.001f);
    }

    [Fact]
    public void Normalized_WrapsAbovePi()
    {
        // 270° = 3π/2, normalized should become -π/2 (= -90°)
        float inputRadians = 3f * MathF.PI / 2f;
        float expectedRadians = -MathF.PI / 2f;

        var angle = Angle.FromRadians(inputRadians).Normalized();

        angle.Radians.ShouldBe(expectedRadians, tolerance: 0.00001f);
    }

    [Fact]
    public void ToVector2_ZeroAnglePointsRight()
    {
        float expectedX = 1f;
        float expectedY = 0f;

        var vec = Angle.FromRadians(0f).ToVector2();

        vec.X.ShouldBe(expectedX, tolerance: 0.00001f);
        vec.Y.ShouldBe(expectedY, tolerance: 0.00001f);
    }

    [Fact]
    public void ToVector2_90DegreesPointsUp()
    {
        float expectedX = 0f;
        float expectedY = 1f;

        var vec = Angle.FromDegrees(90f).ToVector2();

        vec.X.ShouldBe(expectedX, tolerance: 0.00001f);
        vec.Y.ShouldBe(expectedY, tolerance: 0.00001f);
    }
}
