using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests that the bottom-preview zoom combo box stays in sync with the
/// PreviewControl's actual zoom when the user mouse-wheels over the preview.
///
/// Issue #109 — before this fix, wheel-zooming the bottom preview changed
/// the camera but left the PreviewZoomCombo selection stale.
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
        ctx.AppState.UnitType                     = UnitType.Pixel;
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
    public void PreviewZoomCombo_DisplaysExactPercent_AfterWheelZoomOnPreview()
    {
        var ctx = ResetSingletons();

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var combo   = FindCtrl<AutoCompleteBox>(window, "PreviewZoomCombo");

        // One wheel-in notch from 100 % lands at 125 % — explicitly NOT in the
        // preset list { 10, 25, 50, 100, 200, 400 }. The combo must display
        // the live value, not snap to "100%".
        preview.SimulateWheelZoom(100, 100, zoomIn: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("125%", combo.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void PreviewZoomPlusBtn_StepsToNextPresetAbove_FromBetweenPresets()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var combo   = FindCtrl<AutoCompleteBox>(window, "PreviewZoomCombo");
        var plusBtn = FindCtrl<Button>(window, "PreviewZoomPlusBtn");

        // 100 → 125 (between 100 and 200). + must jump to 200, not back to 100.
        preview.SimulateWheelZoom(100, 100, zoomIn: true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("125%", combo.Text);

        // Buttons are wired via the Click event; raise it directly to drive the handler.
        plusBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("200%", combo.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void PreviewZoomMinusBtn_StepsToPreviousPresetBelow_FromBetweenPresets()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview  = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var combo    = FindCtrl<AutoCompleteBox>(window, "PreviewZoomCombo");
        var minusBtn = FindCtrl<Button>(window, "PreviewZoomMinusBtn");

        preview.SimulateWheelZoom(100, 100, zoomIn: true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("125%", combo.Text);

        minusBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // 125 → previous preset strictly less = 100.
        Assert.Equal("100%", combo.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void ZoomPlusBtn_StepsFromExactPreset_GoesToNextPreset()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var wireframe = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var combo     = FindCtrl<AutoCompleteBox>(window, "ZoomCombo");
        var plusBtn   = FindCtrl<Button>(window, "ZoomPlusBtn");

        wireframe.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        plusBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // From exactly 100 (a preset), + must jump to the strictly-greater one (200), not stay at 100.
        Assert.Equal("200%", combo.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void ZoomCombo_DisplaysExactPercent_AfterWheelZoomOnWireframe()
    {
        var ctx = ResetSingletons();

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var wireframe = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var combo     = FindCtrl<AutoCompleteBox>(window, "ZoomCombo");

        wireframe.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        // 100 % × 1.25 = 125 % — same point: should be displayed verbatim,
        // not snapped to "100%" or "200%".
        wireframe.SimulateWheelZoom(50, 50, factor: 1.25f);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("125%", combo.Text);

        window.Close();
    }

    /// <summary>
    /// Issue #110 — WireframeControl must fire ZoomChanged when the user
    /// mouse-wheels over it, so the ZoomCombo in MainWindow can sync its text.
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

    /// <summary>
    /// Issue #110 — the ZoomMinusBtn on the wireframe must step down to the
    /// largest preset strictly less than the current zoom.
    /// Mirrors <see cref="PreviewZoomMinusBtn_StepsToPreviousPresetBelow_FromBetweenPresets"/>.
    /// </summary>
    [AvaloniaFact]
    public void ZoomMinusBtn_StepsToPreviousPresetBelow_FromBetweenPresets()
    {
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var wireframe = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var combo     = FindCtrl<AutoCompleteBox>(window, "ZoomCombo");
        var minusBtn  = FindCtrl<Button>(window, "ZoomMinusBtn");

        // Land at 125 % (between presets 100 and 200).
        wireframe.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();
        wireframe.SimulateWheelZoom(50, 50, factor: 1.25f);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("125%", combo.Text);

        minusBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Previous preset strictly less than 125 is 100.
        Assert.Equal("100%", combo.Text);
        window.Close();
    }
}
