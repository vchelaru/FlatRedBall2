using System;
using System.Numerics;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Math;

public class PathTests
{
    [Fact]
    public void PointAtLength_ArcTo_EndpointMatchesTarget()
    {
        // Verify the arc geometry places the endpoint at the exact target, not just approximately.
        var path = new Path()
            .MoveTo(0, 0)
            .ArcTo(100, 0, MathF.PI);

        var end = path.PointAtLength(path.TotalLength);

        end.X.ShouldBe(100f, tolerance: 0.01f);
        end.Y.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void PointAtLength_LineTo_ReturnsInterpolatedPosition()
    {
        var path = new Path().MoveTo(0, 0).LineTo(100, 0);

        var mid = path.PointAtLength(50f);

        mid.X.ShouldBe(50f, tolerance: 0.001f);
        mid.Y.ShouldBe(0f, tolerance: 0.001f);
    }

    [Fact]
    public void PointAtRatio_AtZeroAndOne_ReturnsStartAndEnd()
    {
        var path = new Path().MoveTo(-50, 0).LineTo(50, 0);

        path.PointAtRatio(0f).X.ShouldBe(-50f, tolerance: 0.001f);
        path.PointAtRatio(1f).X.ShouldBe(50f, tolerance: 0.001f);
    }

    [Fact]
    public void TotalLength_LineTo_MatchesEuclideanDistance()
    {
        // Horizontal line of length 200
        var path = new Path().MoveTo(0, 0).LineTo(200, 0);

        path.TotalLength.ShouldBe(200f, tolerance: 0.001f);
    }

    [Fact]
    public void TotalLength_IsLooped_IncludesClosingSegment()
    {
        // Path from (0,0) to (100,0); closing segment back to (0,0) adds 100
        var path = new Path().MoveTo(0, 0).LineTo(100, 0);
        path.IsLooped = true;

        path.TotalLength.ShouldBe(200f, tolerance: 0.001f);
    }
}
