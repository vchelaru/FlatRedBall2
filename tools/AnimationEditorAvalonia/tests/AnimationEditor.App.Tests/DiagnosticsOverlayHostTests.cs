using AnimationEditor.App.Controls;
using Avalonia;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The render-diagnostics overlay host (#514/#695): one <c>DiagnosticsOverlayHost</c> now owns the
/// on/off flag, the rolling draw-time sampler, and the 1 fps idle-repaint timer for both the
/// wireframe/PNG viewport and the preview panel. These cover the host's state machine — the sampler
/// reaches a draw op only while the overlay is on, and the toggle is idempotent — plus that both
/// panels still expose the same public <c>DiagnosticsEnabled</c> seam MainWindow's F3 drives.
/// </summary>
public class DiagnosticsOverlayHostTests
{
    [AvaloniaFact]
    public void ActiveSampler_WhenDisabled_IsNullSoDrawOpsSkipTiming()
    {
        var host = new DiagnosticsOverlayHost(() => { });

        Assert.Null(host.ActiveSampler);
    }

    [AvaloniaFact]
    public void ActiveSampler_WhenEnabled_IsTheSameSamplerAcrossFrames()
    {
        var host = new DiagnosticsOverlayHost(() => { }) { Enabled = true };

        // One sampler per panel: a fresh RollingAverage per frame would reset the average on every
        // repaint and the ms/frame readout would never settle.
        Assert.NotNull(host.ActiveSampler);
        Assert.Same(host.ActiveSampler, host.ActiveSampler);
    }

    [AvaloniaFact]
    public void DiagnosticsEnabled_OnPreviewControl_RoundTrips()
    {
        var ctrl = TestHelpers.BuildServices().CreatePreviewControl();

        ctrl.DiagnosticsEnabled = true;
        Assert.True(ctrl.DiagnosticsEnabled);

        ctrl.DiagnosticsEnabled = false;
        Assert.False(ctrl.DiagnosticsEnabled);
    }

    [AvaloniaFact]
    public void DiagnosticsEnabled_OnTextureViewport_RoundTrips()
    {
        var ctrl = new TextureViewport();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));

        ctrl.DiagnosticsEnabled = true;
        Assert.True(ctrl.DiagnosticsEnabled);

        ctrl.DiagnosticsEnabled = false;
        Assert.False(ctrl.DiagnosticsEnabled);
    }

    [AvaloniaFact]
    public void Enabled_SetToSameValue_DoesNotRequestARepaint()
    {
        int repaints = 0;
        var host = new DiagnosticsOverlayHost(() => repaints++);

        host.Enabled = true;
        host.Enabled = true;

        Assert.Equal(1, repaints);
    }
}
