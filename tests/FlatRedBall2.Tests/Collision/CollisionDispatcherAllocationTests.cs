using System;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Polygon narrow phase runs per tile/hex cell per frame, so it must not allocate.
public class CollisionDispatcherAllocationTests
{
    private static long MeasureAllocatedBytes(Action action)
    {
        for (int i = 0; i < 20; i++) action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void GetSeparationVector_ShapesVsOverlappingPolygon_AllocatesZeroBytes()
    {
        var polygon = Polygon.CreateRectangle(10f, 10f);
        polygon.X = 5f;
        polygon.Y = 3f;
        var rectangle = new AARect { Width = 10f, Height = 10f };
        var circle = new Circle { Radius = 5f, X = 4f };
        // L-shape decomposes into several convex parts.
        var concave = Polygon.FromPoints(new[]
        {
            new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 4f),
            new Vector2(4f, 4f), new Vector2(4f, 10f), new Vector2(0f, 10f),
        });

        long bytes = MeasureAllocatedBytes(() =>
        {
            CollisionDispatcher.CollidesWith(rectangle, polygon);
            CollisionDispatcher.GetSeparationVector(rectangle, polygon);
            CollisionDispatcher.GetSeparationVector(circle, polygon);
            CollisionDispatcher.GetSeparationVector(concave, polygon);
            CollisionDispatcher.GetBounds(polygon);
        });

        bytes.ShouldBe(0);
    }

    [Fact]
    public void GetTilePolygonSeparation_DirectionalTile_AllocatesZeroBytes()
    {
        var tile = Polygon.CreateRectangle(10f, 10f);
        tile.SolidSides = SolidSides.Up;
        var rectangle = new AARect { Width = 10f, Height = 10f, Y = 8f };
        var circle = new Circle { Radius = 5f, Y = 8f };
        var mover = Polygon.CreateRectangle(10f, 10f);
        mover.Y = 8f;

        long bytes = MeasureAllocatedBytes(() =>
        {
            CollisionDispatcher.GetTilePolygonSeparation(rectangle, tile);
            CollisionDispatcher.GetTilePolygonSeparation(circle, tile);
            CollisionDispatcher.GetTilePolygonSeparation(mover, tile);
        });

        bytes.ShouldBe(0);
    }

    [Fact]
    public void GetSeparationVector_AARectVsHexShapes_AllocatesZeroBytes()
    {
        var shapes = new HexShapes(new HexGrid(10f, HexOrientation.FlatTop, Vector2.Zero));
        shapes.AddHexAtCell(new HexCoordinate(0, 0));
        shapes.AddHexAtCell(new HexCoordinate(1, 0));
        var rectangle = new AARect { Width = 8f, Height = 8f, X = 7f };

        long bytes = MeasureAllocatedBytes(() =>
        {
            shapes.CollidesWith(rectangle);
            shapes.GetSeparationVector(rectangle);
        });

        bytes.ShouldBe(0);
    }
}
