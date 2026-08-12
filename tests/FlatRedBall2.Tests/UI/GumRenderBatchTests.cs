using FlatRedBall2.Rendering;
using FlatRedBall2.UI;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.UI;

// Issue #798: GumRenderBatch.Instance drives Gum's render zoom from Camera.PixelsPerUnit, which
// bakes in Camera.Zoom — correct for normal per-camera HUD (zoom-coupled by design), but wrong for
// screen-space HUD, which must track the window-vs-design-resolution scale only.
public class GumRenderBatchTests
{
    [Fact]
    public void ResolveZoom_Instance_IncludesCameraZoom()
    {
        var camera = new Camera();
        camera.ApplyToHostRect(new Viewport(0, 0, 1280, 720), orthogonalHeight: 720);
        camera.Zoom = 2f;

        GumRenderBatch.Instance.ResolveZoom(camera).ShouldBe(2f, tolerance: 0.001f);
    }

    [Fact]
    public void ResolveZoom_ScreenSpaceInstance_AppliesWindowScaleButIgnoresCameraZoom()
    {
        // 2x window-vs-design-resolution scale (1440 viewport height / 720 orthogonal height), with
        // Zoom=3 that must NOT show up in the result — if it leaked in, this would be 6f, not 2f.
        var camera = new Camera();
        camera.ApplyToHostRect(new Viewport(0, 0, 2560, 1440), orthogonalHeight: 720);
        camera.Zoom = 3f;

        GumRenderBatch.ScreenSpaceInstance.ResolveZoom(camera).ShouldBe(2f, tolerance: 0.001f);
    }
}

// Issue #824: Gum's own cursor hit-testing (InteractiveGue.DoUiActivityRecursively) falls back to
// RenderingLibrary.Camera.ScreenToWorld for FRB2-hosted UI, which reads Zoom AND
// ClientWidth/ClientHeight/ClientLeft/ClientTop. GumRenderBatch.Begin only ever synced Zoom, so
// hit-testing drifted from the rendered position whenever window size != design resolution.
public class GumRenderBatchSyncCursorCameraTests
{
    [Fact]
    public void SyncCursorCamera_WindowLargerThanDesignResolution_SyncsZoomAndClientBounds()
    {
        var camera = new Camera();
        camera.ApplyToHostRect(new Viewport(0, 0, 3840, 2160), orthogonalHeight: 720);
        var gumCamera = new RenderingLibrary.Camera();

        GumRenderBatch.Instance.SyncCursorCamera(gumCamera, camera);

        gumCamera.Zoom.ShouldBe(3f, tolerance: 0.001f);
        gumCamera.ClientWidth.ShouldBe(3840);
        gumCamera.ClientHeight.ShouldBe(2160);
        gumCamera.ClientLeft.ShouldBe(0);
        gumCamera.ClientTop.ShouldBe(0);
    }

    [Fact]
    public void SyncCursorCamera_NonZeroOriginViewport_SyncsClientLeftAndTop()
    {
        var camera = new Camera();
        camera.ApplyToHostRect(new Viewport(100, 50, 1280, 720), orthogonalHeight: 720);
        var gumCamera = new RenderingLibrary.Camera();

        GumRenderBatch.Instance.SyncCursorCamera(gumCamera, camera);

        gumCamera.ClientLeft.ShouldBe(100);
        gumCamera.ClientTop.ShouldBe(50);
    }

    [Fact]
    public void SyncCursorCamera_WindowMatchesDesignResolution_ZoomIsOne()
    {
        var camera = new Camera();
        camera.ApplyToHostRect(new Viewport(0, 0, 1280, 720), orthogonalHeight: 720);
        var gumCamera = new RenderingLibrary.Camera();

        GumRenderBatch.Instance.SyncCursorCamera(gumCamera, camera);

        gumCamera.Zoom.ShouldBe(1f, tolerance: 0.001f);
        gumCamera.ClientWidth.ShouldBe(1280);
        gumCamera.ClientHeight.ShouldBe(720);
    }
}
