using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Hovering (or dragging) a polygon vertex in the preview highlights that vertex's X and Y boxes
/// in the inspector, so it is clear which row a click would edit.
/// </summary>
public class PolygonVertexHoverHighlightTests
{
    private static (TestServices Ctx, MainWindow Window, PreviewControl Preview, Point Center) Build()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var polygon = new PolygonSave { Name = "Blade" };
        foreach (var (x, y) in new[] { (-40f, -40f), (40f, -40f), (40f, 40f), (-40f, 40f), (-40f, -40f) })
            polygon.Points.Add(new Vector2Save { X = x, Y = y });
        var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave!.Shapes.Add(polygon);
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedPolygon = polygon;

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        ctx.SelectedState.SelectedPolygon = polygon; // after the window exists, so the inspector builds its vertex rows
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
        var center = preview.TranslatePoint(
            new Point((preview.Bounds.Width - 20) / 2 + 20, (preview.Bounds.Height - 20) / 2 + 20), window)!.Value;
        return (ctx, window, preview, center);
    }

    private static bool IsRowActive(MainWindow window, int vertex) =>
        new[] { "X", "Y" }.All(axis =>
            window.GetVisualDescendants().OfType<NumericUpDown>()
                .First(n => n.Name == $"PropPolygonVertex{vertex}{axis}")
                .GetVisualParent() is Border { } cell && cell.Classes.Contains("vertexActive"));

    [AvaloniaFact]
    public void HoverVertex_HighlightsThatRowsXAndYBoxes_AndClearsWhenLeaving()
    {
        var (ctx, window, preview, center) = Build();
        try
        {
            float scale = ctx.AppState.OffsetMultiplier * preview.Zoom;
            var onVertex2 = new Point(center.X + 40 * scale, center.Y - 40 * scale); // vertex 2 at (40, 40)

            window.MouseMove(onVertex2);
            Dispatcher.UIThread.RunJobs();

            Assert.True(IsRowActive(window, 2));
            Assert.False(IsRowActive(window, 0));

            window.MouseMove(center); // the polygon's middle, away from every vertex
            Dispatcher.UIThread.RunJobs();

            Assert.False(IsRowActive(window, 2));
        }
        finally { window.Close(); }
    }
}
