using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using AnimationEditor.Core.Utilities;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Deleting a polygon vertex in the preview: hover it and press Delete. Double-clicking a vertex
/// no longer deletes; Delete away from any vertex still deletes the shape.
/// </summary>
public class PolygonVertexDeleteTests
{
    private static Point Vertex2(TestServices ctx, AnimationEditor.App.Controls.PreviewControl preview, Point center)
    {
        float scale = ctx.AppState.OffsetMultiplier * preview.Zoom;
        return new Point(center.X + 40 * scale, center.Y - 40 * scale); // vertex 2 of the 80x80 square
    }

    [AvaloniaFact]
    public void HoverVertex_ThenDelete_RemovesThatVertexOnly()
    {
        var (ctx, window, preview, center, polygon, frame) = PolygonVertexHoverHighlightTests.Build();
        try
        {
            window.MouseMove(Vertex2(ctx, preview, center));
            Dispatcher.UIThread.RunJobs();

            window.HandleDeleteForTest();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(3, PolygonVertices.Count(polygon));
            Assert.Contains(polygon, frame.ShapesSave!.PolygonSaves);
            Assert.DoesNotContain(Enumerable.Range(0, 3).Select(i => PolygonVertices.Get(polygon, i)), v => v == (40f, 40f));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HoverVertexOfTriangle_Delete_KeepsPolygonAndVertices()
    {
        var (ctx, window, preview, center, polygon, frame) =
            PolygonVertexHoverHighlightTests.Build((-40f, -40f), (40f, -40f), (40f, 40f));
        try
        {
            window.MouseMove(Vertex2(ctx, preview, center));
            Dispatcher.UIThread.RunJobs();

            window.HandleDeleteForTest();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(3, PolygonVertices.Count(polygon));
            Assert.Contains(polygon, frame.ShapesSave!.PolygonSaves); // not the whole shape
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DoubleClickVertex_NoLongerDeletesIt()
    {
        var (ctx, window, preview, center, polygon, _) = PolygonVertexHoverHighlightTests.Build();
        try
        {
            var at = Vertex2(ctx, preview, center);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            window.MouseDown(at, MouseButton.Left); // back-to-back: ClickCount 2
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(4, PolygonVertices.Count(polygon));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Delete_NotOverAnyVertex_DeletesTheShape()
    {
        var (ctx, window, preview, center, polygon, frame) = PolygonVertexHoverHighlightTests.Build();
        try
        {
            window.MouseMove(center); // the polygon's middle
            Dispatcher.UIThread.RunJobs();

            window.HandleDeleteForTest();
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(polygon, frame.ShapesSave!.PolygonSaves);
        }
        finally { window.Close(); }
    }
}
