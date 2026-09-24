using System;
using System.Numerics;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Math;

public class HexGridTests
{
    [Fact]
    public void GetCellCenter_PointyTopWithOrigin_ReturnsExpectedCenter()
    {
        var origin = new Vector2(10f, -5f);
        var coordinate = new HexCoordinate(1, 2);
        var grid = new HexGrid(10f, HexOrientation.PointyTop, origin);
        var expected = new Vector2(10f + 20f * MathF.Sqrt(3f), 25f);

        var actual = grid.GetCellCenter(coordinate);
                actual.X.ShouldBe(expected.X, tolerance: 0.001f);
                actual.Y.ShouldBe(expected.Y, tolerance: 0.001f);
    }

    [Fact]
    public void GetCellAt_CentersInBothOrientations_ReturnsOriginalCoordinate()
    {
        var coordinate = new HexCoordinate(-2, 3);
        var flat = new HexGrid(10f, HexOrientation.FlatTop, new Vector2(7f, -11f));
        var pointy = new HexGrid(10f, HexOrientation.PointyTop, new Vector2(7f, -11f));
        var expected = coordinate;

        flat.GetCellAt(flat.GetCellCenter(coordinate)).ShouldBe(expected);
        pointy.GetCellAt(pointy.GetCellCenter(coordinate)).ShouldBe(expected);
    }

    [Fact]
    public void GetCellCorners_PointyTop_ReturnsCounterClockwiseVertices()
    {
        var coordinate = new HexCoordinate(0, 0);
        var grid = new HexGrid(10f, HexOrientation.PointyTop, Vector2.Zero);
        var expectedFirst = new Vector2(5f * MathF.Sqrt(3f), 5f);
        var expectedSecond = new Vector2(0f, 10f);

        var corners = grid.GetCellCorners(coordinate);

        corners.Count.ShouldBe(6);
        corners[0].X.ShouldBe(expectedFirst.X, tolerance: 0.001f);
        corners[0].Y.ShouldBe(expectedFirst.Y, tolerance: 0.001f);
        corners[1].X.ShouldBe(expectedSecond.X, tolerance: 0.001f);
        corners[1].Y.ShouldBe(expectedSecond.Y, tolerance: 0.001f);
    }

    [Fact]
    public void HexCoordinate_DistanceAndNeighbors_ReturnsAxialValues()
    {
        var coordinate = new HexCoordinate(2, -1);
        var other = new HexCoordinate(-1, 3);
        int expectedDistance = 4;

        coordinate.GetNeighbors().Count.ShouldBe(6);
        coordinate.GetNeighbors().ShouldContain(new HexCoordinate(3, -1));
        coordinate.DistanceTo(other).ShouldBe(expectedDistance);
    }

    [Fact]
    public void HexCoordinate_GetNeighbors_ReturnsCounterClockwiseFromPositiveQ()
    {
        var coordinate = new HexCoordinate(0, 0);
        var expected = new[]
        {
            new HexCoordinate(1, 0),
            new HexCoordinate(0, 1),
            new HexCoordinate(-1, 1),
            new HexCoordinate(-1, 0),
            new HexCoordinate(0, -1),
            new HexCoordinate(1, -1),
        };

        coordinate.GetNeighbors().ShouldBe(expected);
    }

    [Fact]
    public void HexGrid_ZeroRadius_ThrowsArgumentOutOfRangeException()
    {
        float radius = 0f;
        var orientation = HexOrientation.FlatTop;
        var origin = Vector2.Zero;

        Should.Throw<ArgumentOutOfRangeException>(() => new HexGrid(radius, orientation, origin));
    }
}
