using System.Linq;
using FlatRedBall2.Rendering;
using FlatRedBall2.UI;
using Gum.GueDeriving;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.UI;

// Gum's GumBatch path never bakes IsRenderTarget containers (vchelaru/Gum#6134), so FRB2 bakes them
// itself before each camera/overlay draw pass. These tests cover which Gum roots get baked; the baked
// pixels need a GraphicsDevice and are covered by the golden-image scene.
public class GumRenderTargetBakeTests
{
    private class TestScreen : Screen { }

    [Fact]
    public void GumRenderablesToBake_CameraHud_OnlyReturnedForOwningCamera()
    {
        var screen = new TestScreen();
        var second = new Camera();
        screen.Cameras.Add(second);
        var hud = new ContainerRuntime();
        screen.Cameras[0].Add(hud);

        screen.GumRenderablesToBake(screen.Cameras[0]).Select(r => r.Visual).ShouldContain(hud);
        screen.GumRenderablesToBake(second).Select(r => r.Visual).ShouldNotContain(hud);
    }

    [Fact]
    public void GumRenderablesToBake_InvisibleVisual_NotReturned()
    {
        var screen = new TestScreen();
        var hud = new ContainerRuntime { Visible = false };
        screen.Add(hud);

        screen.GumRenderablesToBake(screen.Cameras[0]).Select(r => r.Visual).ShouldNotContain(hud);
    }

    [Fact]
    public void GumOverlayRenderablesToBake_OnlyOverlayRootsWithContent()
    {
        var screen = new TestScreen();
        var populated = new ContainerRuntime();
        populated.Children.Add(new ContainerRuntime());
        var empty = new ContainerRuntime();
        screen.AddOverlayRoot(populated);
        screen.AddOverlayRoot(empty);

        var roots = screen.GumOverlayRenderablesToBake().Select(r => r.Visual).ToList();

        roots.ShouldContain(populated);
        roots.ShouldNotContain(empty);
    }

    [Fact]
    public void GumOverlayRenderablesToBake_CameraHud_NotReturned()
    {
        var screen = new TestScreen();
        var hud = new ContainerRuntime();
        screen.Add(hud);

        screen.GumOverlayRenderablesToBake().Select(r => r.Visual).ShouldNotContain(hud);
    }
}
