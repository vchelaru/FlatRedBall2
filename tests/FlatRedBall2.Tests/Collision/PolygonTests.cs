using System;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class PolygonTests
{
    [Fact]
    public void SetPoints_ReplacesAllExistingPoints()
    {
        var polygon = Polygon.FromPoints(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(5, 10) });

        polygon.SetPoints(new[] { new Vector2(1, 2), new Vector2(3, 4) });

        polygon.Points.Count.ShouldBe(2);
        polygon.Points[0].ShouldBe(new Vector2(1, 2));
        polygon.Points[1].ShouldBe(new Vector2(3, 4));
    }

    [Fact]
    public void SetPoints_EmptySequence_ClearsAllPoints()
    {
        var polygon = Polygon.FromPoints(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(5, 10) });

        polygon.SetPoints(Array.Empty<Vector2>());

        polygon.Points.ShouldBeEmpty();
    }
}
