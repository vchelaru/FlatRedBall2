using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class FlatRedBallServiceTests
{
    [Fact]
    public void ApplyClientSizeChange_AllowUserResizingFalse_LeavesCameraViewportUnchanged()
    {
        // Repro for KNI BlazorGL fixed-size canvas: when AllowUserResizing is false, browser-window
        // resizes echo through ClientSizeChanged with the browser's dimensions even though the canvas
        // DOM is pinned. The engine must ignore the event so the camera viewport stays bound to the
        // host-managed surface.
        var engine = new FlatRedBallService();
        var camera = new Camera();
        camera.SetViewport(new Viewport(0, 0, 720, 960));
        camera.TargetWidth = 720;
        camera.TargetHeight = 960;

        engine.ApplyClientSizeChange(1920, 1080, allowUserResizing: false, camera);

        camera.Viewport.Width.ShouldBe(720);
        camera.Viewport.Height.ShouldBe(960);
        camera.TargetWidth.ShouldBe(720);
        camera.TargetHeight.ShouldBe(960);
    }

    [Fact]
    public void ApplyClientSizeChange_AllowUserResizingTrue_RecomputesCameraViewport()
    {
        // Counter-test: stretch-to-viewport behavior must remain intact when the host opts into
        // user resizing (default desktop, KNI canvas-stretch mode).
        var engine = new FlatRedBallService();
        engine.DisplaySettings.ResizeMode = ResizeMode.IncreaseVisibleArea;
        var camera = new Camera();
        camera.SetViewport(new Viewport(0, 0, 720, 960));

        engine.ApplyClientSizeChange(1920, 1080, allowUserResizing: true, camera);

        camera.Viewport.Width.ShouldBe(1920);
        camera.Viewport.Height.ShouldBe(1080);
        camera.TargetWidth.ShouldBe(1920);
        camera.TargetHeight.ShouldBe(1080);
    }
}
