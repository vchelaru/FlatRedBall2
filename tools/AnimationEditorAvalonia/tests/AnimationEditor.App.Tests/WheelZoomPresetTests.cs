using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #213 — mouse-wheel zoom now steps through the same preset list used by
/// the +/− buttons, so it always lands on a round-number zoom level.
///
/// Covers:
///  • <see cref="ZoomPresetStepper"/> unit tests (float precision, edge cases)
///  • WireframeControl and PreviewControl wheel-zoom integration via MainWindow
/// </summary>
public class WheelZoomPresetTests
{
    // ── ZoomPresetStepper unit tests ──────────────────────────────────────────

    private static readonly int[] _samplePresets = { 50, 75, 100, 150, 200 };

    [Fact]
    public void StepToNextPreset_ZoomIn_FromExactPreset_GoesToNextPreset()
    {
        Assert.Equal(150, ZoomPresetStepper.StepToNextPreset(100f, _samplePresets, +1));
    }

    [Fact]
    public void StepToNextPreset_ZoomOut_FromExactPreset_GoesToPreviousPreset()
    {
        Assert.Equal(75, ZoomPresetStepper.StepToNextPreset(100f, _samplePresets, -1));
    }

    [Fact]
    public void StepToNextPreset_ZoomIn_FromBetweenPresets_GoesToNextAbove()
    {
        Assert.Equal(150, ZoomPresetStepper.StepToNextPreset(125f, _samplePresets, +1));
    }

    [Fact]
    public void StepToNextPreset_ZoomOut_FromBetweenPresets_GoesToNextBelow()
    {
        Assert.Equal(100, ZoomPresetStepper.StepToNextPreset(125f, _samplePresets, -1));
    }

    [Fact]
    public void StepToNextPreset_ZoomIn_AtMaxPreset_ClampsToMax()
    {
        Assert.Equal(200, ZoomPresetStepper.StepToNextPreset(200f, _samplePresets, +1));
    }

    [Fact]
    public void StepToNextPreset_ZoomOut_AtMinPreset_ClampsToMin()
    {
        Assert.Equal(50, ZoomPresetStepper.StepToNextPreset(50f, _samplePresets, -1));
    }

    [Fact]
    public void StepToNextPreset_ZoomIn_FloatDriftBelowPreset_AdvancesNotStalls()
    {
        // 99.99998 is below 100 by less than epsilon; must snap to 100 and advance to 150.
        Assert.Equal(150, ZoomPresetStepper.StepToNextPreset(99.99998f, _samplePresets, +1));
    }

    [Fact]
    public void StepToNextPreset_ZoomOut_FloatDriftAbovePreset_AdvancesNotStalls()
    {
        // 100.00002 is above 100 by less than epsilon; must snap to 100 and step back to 75.
        Assert.Equal(75, ZoomPresetStepper.StepToNextPreset(100.00002f, _samplePresets, -1));
    }

    [Fact]
    public void StepToNextPreset_ZoomIn_FloatDrift_NonRoundPreset()
    {
        // 33% and 66% are tricky (not exactly representable as float).
        // 32.9999 is near 33 — must snap and advance to 50.
        var presets = new[] { 33, 50, 66, 75 };
        Assert.Equal(50, ZoomPresetStepper.StepToNextPreset(32.9999f, presets, +1));
    }

    [Fact]
    public void StepToNextPreset_ZoomOut_FloatDrift_NonRoundPreset()
    {
        var presets = new[] { 33, 50, 66, 75 };
        Assert.Equal(50, ZoomPresetStepper.StepToNextPreset(66.00001f, presets, -1));
    }

    [Fact]
    public void StepToNextPreset_SingleElement_ClampsInBothDirections()
    {
        // The guard is at the call site (WheelZoomPresets is { Length: > 0 }), but
        // test the stepper itself with a single-element array for completeness.
        var single = new[] { 100 };
        Assert.Equal(100, ZoomPresetStepper.StepToNextPreset(100f, single, +1));
        Assert.Equal(100, ZoomPresetStepper.StepToNextPreset(100f, single, -1));
    }

    // ── Integration tests via MainWindow ─────────────────────────────────────

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

    [AvaloniaFact]
    public void WireframeWheel_ZoomIn_FromPreset_LandsOnNextPreset()
    {
        var ctx    = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var ctrl  = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var combo = FindCtrl<AutoCompleteBox>(window, "ZoomCombo");

        ctrl.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        ctrl.SimulateWheelZoom(50f, 50f, zoomIn: true);
        Dispatcher.UIThread.RunJobs();

        // Next preset above 100 is 150.
        Assert.Equal("150%", combo.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void WireframeWheel_ZoomOut_FromPreset_LandsOnPreviousPreset()
    {
        var ctx    = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var ctrl  = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var combo = FindCtrl<AutoCompleteBox>(window, "ZoomCombo");

        ctrl.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        ctrl.SimulateWheelZoom(50f, 50f, zoomIn: false);
        Dispatcher.UIThread.RunJobs();

        // Previous preset below 100 is 75.
        Assert.Equal("75%", combo.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void WireframeWheel_MultipleStepsIn_PassThroughPresets()
    {
        var ctx    = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var ctrl  = FindCtrl<WireframeControl>(window, "WireframeCtrl");
        var combo = FindCtrl<AutoCompleteBox>(window, "ZoomCombo");

        ctrl.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        // 100 → 150 → 200 (two notches always land on presets).
        ctrl.SimulateWheelZoom(50f, 50f, zoomIn: true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("150%", combo.Text);

        ctrl.SimulateWheelZoom(50f, 50f, zoomIn: true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("200%", combo.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void PreviewWheel_ZoomIn_FromPreset_LandsOnNextPreset()
    {
        var ctx    = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var combo   = FindCtrl<AutoCompleteBox>(window, "PreviewZoomCombo");

        preview.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        preview.SimulateWheelZoom(100, 100, zoomIn: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("150%", combo.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void PreviewWheel_ZoomOut_FromPreset_LandsOnPreviousPreset()
    {
        var ctx    = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = FindCtrl<PreviewControl>(window, "PreviewCtrl");
        var combo   = FindCtrl<AutoCompleteBox>(window, "PreviewZoomCombo");

        preview.SetZoomPercent(100);
        Dispatcher.UIThread.RunJobs();

        preview.SimulateWheelZoom(100, 100, zoomIn: false);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("75%", combo.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void WireframeWheel_StandaloneControl_NoPresets_UsesMultiplier()
    {
        // A WireframeControl created without MainWindow has WheelZoomPresets = null,
        // so it falls back to the 1.25× multiplier (legacy behaviour).
        var ctx  = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));

        float? lastPct = null;
        ctrl.ZoomChanged += pct => lastPct = pct;

        ctrl.SimulateWheelZoom(50f, 50f, factor: 1.25f);

        Assert.NotNull(lastPct);
        Assert.Equal(125f, lastPct!.Value, precision: 2);
    }
}
