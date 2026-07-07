using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests that the shared <see cref="ZoomControl"/> stays in sync with its bound view's actual
/// zoom (wheel, +/- steps) for both the wireframe and the bottom preview.
///
/// Issue #109 — before this fix, wheel-zooming a view changed the camera but left the zoom
/// field stale. The three zoom widgets now share one <see cref="ZoomControl"/> implementation.
/// </summary>
public class PreviewZoomComboSyncTests
{
    private static TestServices ResetSingletons() {
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

    [AvaloniaFact]
    public void PreviewControl_FiresZoomChanged_OnWheelZoom()
    {
        var ctx = ResetSingletons();
        var preview = ctx.CreatePreviewControl();
        // Force a non-zero bounds so ApplyWheelZoom math is exercised.
        preview.Measure(new Size(400, 300));
        preview.Arrange(new Rect(0, 0, 400, 300));

        float? lastPct = null;
        preview.ZoomChanged += pct => lastPct = pct;

        preview.SimulateWheelZoom(100, 100, zoomIn: true);

        Assert.NotNull(lastPct);
        // 1.0 * 1.25 = 1.25 → 125 %
        Assert.Equal(125f, lastPct!.Value, precision: 2);
    }

    [AvaloniaFact]
    public void PreviewZoom_DisplaysExactPercent_AfterWheelZoomOnPreview()
    {
        var ctx = ResetSingletons();

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var zoom    = FindCtrl<ZoomControl>(window, "PreviewZoom");

        // One wheel-in notch from 100 % steps to the next preset (150 %).
        // The field must display the live value exactly.
        preview.SimulateWheelZoom(100, 100, zoomIn: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("150%", zoom.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void PreviewZoom_StepDown_FromBetweenPresets_StepsToPreviousPresetBelow()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var zoom    = FindCtrl<ZoomControl>(window, "PreviewZoom");

        preview.SimulateWheelZoom(100, 100, zoomIn: true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("150%", zoom.Text);

        zoom.StepDown();
        Dispatcher.UIThread.RunJobs();

        // 150 → previous preset strictly less = 100.
        Assert.Equal("100%", zoom.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void PreviewZoom_StepUp_FromBetweenPresets_StepsToNextPresetAbove()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var zoom    = FindCtrl<ZoomControl>(window, "PreviewZoom");

        // 100 → 150 (next preset above 100). StepUp must jump to 200, not back to 100.
        preview.SimulateWheelZoom(100, 100, zoomIn: true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("150%", zoom.Text);

        zoom.StepUp();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("200%", zoom.Text);
        window.Close();
    }

    /// <summary>
    /// Issue #110 — WireframeControl must fire ZoomChanged when the user
    /// mouse-wheels over it, so the bound <see cref="ZoomControl"/> can sync its text.
    /// Mirrors <see cref="PreviewControl_FiresZoomChanged_OnWheelZoom"/>.
    /// </summary>
    [AvaloniaFact]
    public void WireframeControl_FiresZoomChanged_OnWheelZoom()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));

        float? lastPct = null;
        ctrl.ZoomChanged += pct => lastPct = pct;

        // factor 1.25 = one wheel-in notch; 1.0 × 1.25 = 1.25 → 125 %
        ctrl.SimulateWheelZoom(50, 50, factor: 1.25f);

        Assert.NotNull(lastPct);
        Assert.Equal(125f, lastPct!.Value, precision: 2);
    }

    [AvaloniaFact]
    public void WireframeZoom_DisplaysExactPercent_AfterWheelZoomOnWireframe()
    {
        var ctx = ResetSingletons();

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var wireframe = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var zoom      = FindCtrl<ZoomControl>(window, "WireframeZoom");

        wireframe.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        // 100 % × 1.25 = 125 % — same point: should be displayed verbatim,
        // not snapped to "100%" or "200%".
        wireframe.SimulateWheelZoom(50, 50, factor: 1.25f);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("125%", zoom.Text);

        window.Close();
    }

    /// <summary>
    /// Issue #110 — StepDown on the wireframe zoom must step down to the
    /// largest preset strictly less than the current zoom.
    /// Mirrors <see cref="PreviewZoom_StepDown_FromBetweenPresets_StepsToPreviousPresetBelow"/>.
    /// </summary>
    [AvaloniaFact]
    public void WireframeZoom_StepDown_FromBetweenPresets_StepsToPreviousPresetBelow()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var wireframe = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var zoom      = FindCtrl<ZoomControl>(window, "WireframeZoom");

        // Land at 125 % (between presets 100 and 200).
        wireframe.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();
        wireframe.SimulateWheelZoom(50, 50, factor: 1.25f);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("125%", zoom.Text);

        zoom.StepDown();
        Dispatcher.UIThread.RunJobs();

        // Previous preset strictly less than 125 is 100.
        Assert.Equal("100%", zoom.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void WireframeZoom_StepUp_FromExactPreset_GoesToNextPreset()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var wireframe = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var zoom      = FindCtrl<ZoomControl>(window, "WireframeZoom");

        wireframe.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        zoom.StepUp();
        Dispatcher.UIThread.RunJobs();

        // From exactly 100 (a preset), StepUp must jump to the strictly-greater one (150), not stay at 100.
        Assert.Equal("150%", zoom.Text);
        window.Close();
    }
}
