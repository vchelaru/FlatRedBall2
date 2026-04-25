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
        camera.OrthogonalWidth = 720;
        camera.OrthogonalHeight = 960;

        engine.ApplyClientSizeChange(1920, 1080, allowUserResizing: false, camera);

        camera.Viewport.Width.ShouldBe(720);
        camera.Viewport.Height.ShouldBe(960);
        camera.OrthogonalWidth.ShouldBe(720);
        camera.OrthogonalHeight.ShouldBe(960);
    }

    [Fact]
    public void ApplyClientSizeChange_LockedAspectStretch_PreservesDesignWorldExtents()
    {
        // The ShmupSpace bug: under Locked aspect + StretchVisibleArea, resizing the window must
        // NOT widen the playfield. World extents stay at ResolutionWidth/Height; the rendered
        // viewport just gets pillarboxed and rescaled.
        var engine = new FlatRedBallService();
        engine.DisplaySettings.AspectPolicy = AspectPolicy.Locked;
        engine.DisplaySettings.ResizeMode = ResizeMode.StretchVisibleArea;
        engine.DisplaySettings.ResolutionWidth = 240;
        engine.DisplaySettings.ResolutionHeight = 320;
        var camera = new Camera();

        engine.ApplyClientSizeChange(1500, 1000, allowUserResizing: true, camera);

        // 0.75 design ratio inside 1.5 window ratio → pillarbox: viewport height = 1000, width = 750
        camera.Viewport.Height.ShouldBe(1000);
        camera.Viewport.Width.ShouldBe(750);
        // World visible stays at the design — the playfield doesn't grow.
        camera.OrthogonalWidth.ShouldBe(240);
        camera.OrthogonalHeight.ShouldBe(320);
    }

    [Fact]
    public void ApplyClientSizeChange_LockedAspectIncrease_GrowsWorldWithViewport()
    {
        // Locked aspect + IncreaseVisibleArea: pixels-per-world-unit fixed (= Zoom). A bigger
        // window reveals more world along both axes proportionally (aspect stays locked).
        var engine = new FlatRedBallService();
        engine.DisplaySettings.AspectPolicy = AspectPolicy.Locked;
        engine.DisplaySettings.ResizeMode = ResizeMode.IncreaseVisibleArea;
        engine.DisplaySettings.ResolutionWidth = 240;
        engine.DisplaySettings.ResolutionHeight = 320;
        var camera = new Camera();

        engine.ApplyClientSizeChange(1500, 1000, allowUserResizing: true, camera);

        camera.Viewport.Width.ShouldBe(750);
        camera.Viewport.Height.ShouldBe(1000);
        // World extents track the viewport pixels (PixelsPerUnit = Zoom regardless of size).
        camera.OrthogonalWidth.ShouldBe(750);
        camera.OrthogonalHeight.ShouldBe(1000);
    }

    [Fact]
    public void ApplyClientSizeChange_FreeDominantHeightStretch_GrowsWorldWidthOnly()
    {
        // Free + DominantHeight + Stretch: resize wider reveals more world horizontally;
        // height stays at ResolutionHeight regardless of window size.
        var engine = new FlatRedBallService();
        engine.DisplaySettings.AspectPolicy = AspectPolicy.Free;
        engine.DisplaySettings.DominantAxis = DominantAxis.Height;
        engine.DisplaySettings.ResizeMode = ResizeMode.StretchVisibleArea;
        engine.DisplaySettings.ResolutionWidth = 1280;
        engine.DisplaySettings.ResolutionHeight = 720;
        var camera = new Camera();

        engine.ApplyClientSizeChange(1920, 1080, allowUserResizing: true, camera);

        camera.Viewport.Width.ShouldBe(1920);
        camera.Viewport.Height.ShouldBe(1080);
        // OrthogonalHeight pinned to ResolutionHeight; OrthogonalWidth derived from window aspect.
        camera.OrthogonalHeight.ShouldBe(720);
        // 720 * (1920/1080) = 1280
        camera.OrthogonalWidth.ShouldBe(1280);
    }

    [Fact]
    public void ApplyClientSizeChange_FreeIncreaseVisibleArea_GrowsWorldOnBothAxes()
    {
        // Free + IncreaseVisibleArea: pixels-per-world-unit fixed; bigger window = more world both ways.
        var engine = new FlatRedBallService();
        engine.DisplaySettings.AspectPolicy = AspectPolicy.Free;
        engine.DisplaySettings.ResizeMode = ResizeMode.IncreaseVisibleArea;
        var camera = new Camera();

        engine.ApplyClientSizeChange(1920, 1080, allowUserResizing: true, camera);

        camera.Viewport.Width.ShouldBe(1920);
        camera.Viewport.Height.ShouldBe(1080);
        camera.OrthogonalWidth.ShouldBe(1920);
        camera.OrthogonalHeight.ShouldBe(1080);
    }
}
