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
