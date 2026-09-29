using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

// FRB1 closes a polygon by repeating its first point at the end. PolygonVertices shows the
// editor N unique vertices and keeps that repeated point in sync, so the file round-trips.
public class PolygonVerticesTests
{
    private static PolygonSave Poly(params (float X, float Y)[] points)
    {
        var p = new PolygonSave();
        foreach (var (x, y) in points) p.Points.Add(new Vector2Save { X = x, Y = y });
        return p;
    }

    [Fact]
    public void Count_ClosedPolygon_ExcludesRepeatedFirstPoint()
    {
        var p = Poly((0, 0), (10, 0), (10, 10), (0, 0));

        PolygonVertices.Count(p).ShouldBe(3);
    }

    [Fact]
    public void Count_OpenPolygon_CountsEveryPoint()
    {
        var p = Poly((0, 0), (10, 0), (10, 10));

        PolygonVertices.Count(p).ShouldBe(3);
    }

    [Fact]
    public void Set_FirstVertexOfClosedPolygon_MovesTheClosingPointToo()
    {
        var p = Poly((0, 0), (10, 0), (10, 10), (0, 0));

        PolygonVertices.Set(p, 0, -5, 3);

        p.Points[0].X.ShouldBe(-5f);
        p.Points[3].X.ShouldBe(-5f);
        p.Points[3].Y.ShouldBe(3f);
    }

    [Fact]
    public void Insert_AtEndOfClosedPolygon_GoesBeforeTheClosingPoint()
    {
        var p = Poly((0, 0), (10, 0), (10, 10), (0, 0));

        PolygonVertices.Insert(p, 3, 0, 10);

        p.Points.Count.ShouldBe(5);
        p.Points[3].Y.ShouldBe(10f);
        p.Points[4].X.ShouldBe(0f);
        p.Points[4].Y.ShouldBe(0f);
    }

    [Fact]
    public void RemoveAt_FirstVertexOfClosedPolygon_ClosesOnTheNewFirstVertex()
    {
        var p = Poly((0, 0), (10, 0), (10, 10), (0, 10), (0, 0));

        PolygonVertices.RemoveAt(p, 0);

        PolygonVertices.Count(p).ShouldBe(3);
        p.Points[0].X.ShouldBe(10f);
        p.Points[^1].X.ShouldBe(10f);
        p.Points[^1].Y.ShouldBe(0f);
    }

    [Fact]
    public void IsSelfIntersecting_Bowtie_ReturnsTrue()
    {
        var p = Poly((0, 0), (10, 10), (10, 0), (0, 10), (0, 0));

        PolygonVertices.IsSelfIntersecting(p).ShouldBeTrue();
    }

    [Fact]
    public void IsSelfIntersecting_ClosedConcaveL_ReturnsFalse()
    {
        var p = Poly((0, 0), (20, 0), (20, 10), (10, 10), (10, 20), (0, 20), (0, 0));

        PolygonVertices.IsSelfIntersecting(p).ShouldBeFalse();
    }
}
