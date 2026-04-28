using FlatRedBall2.Rendering;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

// Per-camera HUD ownership and the screen-level overlay layer.
// These tests assert the API state (parenting, draw skipping) without driving the
// MonoGame draw pipeline — that requires a GraphicsDevice, which is out of scope here.
public class GumHudTests
{
    private class TestScreen : Screen { }

    [Fact]
    public void Add_GraphicalUiElement_SingleCameraScreen_VisualParentedToCamerasZeroUiRoot()
    {
        var screen = new TestScreen();
        var visual = new ContainerRuntime();

        screen.Add(visual);

        screen.Cameras[0].UiRoot.Children.ShouldContain(visual);
    }

    [Fact]
    public void CameraAdd_TwoCameras_VisualsParentedToOwningCameraUiRoot()
    {
        var screen = new TestScreen();
        var second = new Camera();
        screen.Cameras.Add(second);
        var a = new ContainerRuntime();
        var b = new ContainerRuntime();

        screen.Cameras[0].Add(a);
        second.Add(b);

        screen.Cameras[0].UiRoot.Children.ShouldContain(a);
        screen.Cameras[0].UiRoot.Children.ShouldNotContain(b);
        second.UiRoot.Children.ShouldContain(b);
        second.UiRoot.Children.ShouldNotContain(a);
    }

    [Fact]
    public void CameraAdd_RenderableSkippedForOtherCameras()
    {
        // The owning-camera filter is what prevents Camera 0's HUD from being drawn under
        // Camera 1's transform. We assert the filter via the internal ShouldDraw check rather
        // than invoking Draw (which needs a GraphicsDevice).
        var screen = new TestScreen();
        var second = new Camera();
        screen.Cameras.Add(second);
        var a = new ContainerRuntime();
        screen.Cameras[0].Add(a);

        var renderable = screen.GumRenderables[0];

        renderable.ShouldDrawForCamera(screen.Cameras[0]).ShouldBeTrue();
        renderable.ShouldDrawForCamera(second).ShouldBeFalse();
    }

    [Fact]
    public void AddOverlay_VisualParentedToOverlayRoot_AndSkippedDuringPerCameraDraw()
    {
        var screen = new TestScreen();
        var visual = new ContainerRuntime();

        screen.AddOverlay(visual);

        screen.OverlayRoot.Children.ShouldContain(visual);
        var renderable = screen.GumRenderables[0];
        // Overlay renderables are drawn in a post-camera pass, never inside the per-camera loop.
        renderable.ShouldDrawForCamera(screen.Cameras[0]).ShouldBeFalse();
    }

}
