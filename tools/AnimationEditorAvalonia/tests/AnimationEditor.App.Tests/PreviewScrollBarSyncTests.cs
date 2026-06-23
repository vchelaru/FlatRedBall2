using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Integration tests for the Preview panel's scrollbars (#415). The scrollbars are driven
/// by the existing manual center-relative pan (no ScrollViewer): dragging a scrollbar pans
/// the preview, and panning/zooming refreshes the scrollbars. Mirrors the wiring pattern in
/// <see cref="PreviewZoomComboSyncTests"/>.
/// </summary>
public class PreviewScrollBarSyncTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name)
           ?? throw new InvalidOperationException($"Control '{name}' not found");

    // Consistency: both panels now use standalone ScrollBars (#415 Preview, #422 Wireframe) that
    // auto-hide (thin when idle, expand on hover).
    [AvaloniaFact]
    public void PreviewAndWireframeScrollBars_AllAutoHide()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(FindCtrl<ScrollBar>(window, "PreviewHScroll").AllowAutoHide);
        Assert.True(FindCtrl<ScrollBar>(window, "PreviewVScroll").AllowAutoHide);
        Assert.True(FindCtrl<ScrollBar>(window, "WireframeHScroll").AllowAutoHide);
        Assert.True(FindCtrl<ScrollBar>(window, "WireframeVScroll").AllowAutoHide);

        window.Close();
    }

    // Dragging the vertical scrollbar pans the preview, inverted (scroll value = -pan).
    [AvaloniaFact]
    public void PreviewVScroll_SetValue_MovesPanInverted()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var vscroll = FindCtrl<ScrollBar>(window, "PreviewVScroll");

        Assert.True(vscroll.Minimum < 0, "expected a non-degenerate scroll range from real bounds");

        vscroll.Value = vscroll.Minimum; // drag the thumb to the top
        Dispatcher.UIThread.RunJobs();

        // pan = -scrollValue
        Assert.Equal(-(float)vscroll.Minimum, preview.PanOffset.Y, 3);
    }

    // Wheel-zooming with offset content grows the on-screen extent, so the scrollbar range
    // grows too — proving the scrollbars refresh on zoom and derive from the content extent.
    [AvaloniaFact]
    public void PreviewVScroll_Range_GrowsAfterWheelZoomIn_WithOffsetContent()
    {
        var ctx = ResetSingletons();
        ctx.AppState.OffsetMultiplier = 1f;

        // A shape 100 world-units above the origin gives the extent something to scale.
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(new CircleSave { X = 0f, Y = 100f, Radius = 50f });
        ctx.SelectedState.SelectedFrame = frame;

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var vscroll = FindCtrl<ScrollBar>(window, "PreviewVScroll");

        preview.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();
        double before = vscroll.Maximum - vscroll.Minimum;

        for (int i = 0; i < 3; i++)
            preview.SimulateWheelZoom(100, 100, zoomIn: true);
        Dispatcher.UIThread.RunJobs();

        double after = vscroll.Maximum - vscroll.Minimum;
        Assert.True(after > before, $"range did not grow with zoom: before={before:F1} after={after:F1}");

        window.Close();
    }
}
