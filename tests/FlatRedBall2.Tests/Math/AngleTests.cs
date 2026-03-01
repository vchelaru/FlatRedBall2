using System;
using System.Numerics;
using FlatRedBall2.Math;
using Xunit;

namespace FlatRedBall2.Tests.Math;

public class AngleTests
{
    [Fact]
    public void FromDegrees_StoresCorrectRadians()
    {
        float expectedRadians = MathF.PI;

        var angle = Angle.FromDegrees(180f);

        Assert.Equal(expectedRadians, angle.Radians, 5);
    }

    [Fact]
    public void FromRadians_StoresCorrectDegrees()
    {
        float expectedDegrees = 90f;

        var angle = Angle.FromRadians(MathF.PI / 2f);

        Assert.Equal(expectedDegrees, angle.Degrees, 4);
    }

    [Fact]
    public void Normalized_WrapsAbovePi()
    {
        // 270° = 3π/2, normalized should become -π/2 (= -90°)
        float inputRadians = 3f * MathF.PI / 2f;
        float expectedRadians = -MathF.PI / 2f;

        var angle = Angle.FromRadians(inputRadians).Normalized();

        Assert.Equal(expectedRadians, angle.Radians, 5);
    }

    [Fact]
    public void ToVector2_ZeroAnglePointsRight()
    {
        float expectedX = 1f;
        float expectedY = 0f;

        var vec = Angle.FromRadians(0f).ToVector2();

        Assert.Equal(expectedX, vec.X, 5);
        Assert.Equal(expectedY, vec.Y, 5);
    }

    [Fact]
    public void Add_SumsRadians()
    {
        float expectedRadians = MathF.PI;

        var result = Angle.FromRadians(MathF.PI / 2f) + Angle.FromRadians(MathF.PI / 2f);

        Assert.Equal(expectedRadians, result.Radians, 5);
    }

    [Fact]
    public void Lerp_MidpointIsHalfway()
    {
        var a = Angle.FromDegrees(0f);
        var b = Angle.FromDegrees(90f);
        float expectedDegrees = 45f;

        var result = Angle.Lerp(a, b, 0.5f);

        Assert.Equal(expectedDegrees, result.Degrees, 3);
    }

    [Fact]
    public void Equality_SameRadiansAreEqual()
    {
        var a = Angle.FromDegrees(45f);
        var b = Angle.FromDegrees(45f);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }
}
