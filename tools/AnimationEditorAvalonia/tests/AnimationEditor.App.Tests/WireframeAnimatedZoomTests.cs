using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Smooth (animated) wheel zoom (#425). The wheel handler now retargets a single
/// <c>_targetZoom</c> and eases toward it via <see cref="ZoomChase"/>, applying each
/// stepped value through the existing pivot-preserving <c>ZoomToward</c>.
///
/// The pure easing is covered headlessly in <c>ZoomChaseTests</c>; these tests cover the
/// control-level wiring: target computation, retargeting in-flight, exact settle, and that
/// the cursor pivot stays anchored for the whole animation (not just at the end).
/// </summary>
public class WireframeAnimatedZoomTests
{
    private static readonly int[] _presets = { 50, 100, 150, 200 };

    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.AppCommands.DoOnUiThread              = a => a();
        return ctx;
    }

    private static WireframeControl MakeControl(TestServices ctx)
    {
        var ctrl = ctx.CreateWireframeControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));
        ctrl.WheelZoomPresets = _presets;
        return ctrl;
    }

    [AvaloniaFact]
    public void SimulateWheelZoomBegin_RapidNotches_RetargetsToAccumulatedPreset()
    {
        var ctrl = MakeControl(ResetSingletons());
        ctrl.SetZoomPercent(100);

        ctrl.SimulateWheelZoomBegin(200f, 150f, zoomIn: true);   // target 150 %
        ctrl.StepZoomAnimation(0.016f);                          // ease partway toward 150 %
        ctrl.SimulateWheelZoomBegin(200f, 150f, zoomIn: true);   // second notch before arrival

        // The second notch must step the TARGET to the next preset (200 %), not re-step
        // from the mid-animation zoom (which would re-target 150 %).
        Assert.Equal(2.0f, ctrl.TargetZoom, precision: 5);

        ctrl.SettleZoomAnimation();
        Assert.Equal(2.0f, ctrl.CameraState.Zoom, precision: 5);
        Assert.False(ctrl.IsZoomAnimating);
    }

    [AvaloniaFact]
    public void SimulateWheelZoomBegin_ZoomIn_TargetsNextPresetAndEasesPartway()
    {
        var ctrl = MakeControl(ResetSingletons());
        ctrl.SetZoomPercent(100);

        ctrl.SimulateWheelZoomBegin(200f, 150f, zoomIn: true);

        Assert.True(ctrl.IsZoomAnimating);
        Assert.Equal(1.5f, ctrl.TargetZoom, precision: 5);

        // One 16 ms tick eases toward — but does not reach — the target.
        ctrl.StepZoomAnimation(0.016f);
        float z = ctrl.CameraState.Zoom;
        Assert.True(z > 1.0f && z < 1.5f, $"expected zoom in (1.0, 1.5) after one tick, got {z}");

        ctrl.SettleZoomAnimation(); // clean up the timer
    }

    [AvaloniaFact]
    public void StepZoomAnimation_PivotStaysAnchoredDuringAnimation()
    {
        var ctrl = MakeControl(ResetSingletons());
        // Known camera: pan (0,0), zoom 1× → texture coord under viewport (200,150) is (200,150).
        ctrl.SetCamera(0f, 0f, 1f);
        const float pivotX = 200f, pivotY = 150f;
        var (texX, texY) = CanvasTransform.ScreenToTexture(pivotX, pivotY, 0f, 0f, 1f);

        ctrl.SimulateWheelZoomBegin(pivotX, pivotY, zoomIn: true);
        ctrl.StepZoomAnimation(0.016f); // mid-animation (zoom now between 1.0 and 1.5)

        var (panX, panY, zoom) = ctrl.CameraState;
        var (texAfterX, texAfterY) = CanvasTransform.ScreenToTexture(pivotX, pivotY, panX, panY, zoom);
        Assert.Equal(texX, texAfterX, precision: 2);
        Assert.Equal(texY, texAfterY, precision: 2);

        ctrl.SettleZoomAnimation();
    }

    [AvaloniaFact]
    public void StepZoomAnimation_RunToCompletion_LandsExactlyOnTargetAndStops()
    {
        var ctrl = MakeControl(ResetSingletons());
        ctrl.SetZoomPercent(100);

        ctrl.SimulateWheelZoomBegin(200f, 150f, zoomIn: true); // target 150 %
        ctrl.SettleZoomAnimation();

        Assert.Equal(1.5f, ctrl.CameraState.Zoom, precision: 5);
        Assert.False(ctrl.IsZoomAnimating);
    }
}
