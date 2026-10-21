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

    // The bake walks the whole Gum tree twice per camera pass (and Gum's Draw walks it again), so a scene
    // with no render targets must skip it: that is every frame of a card board, a HUD, a menu.
    [Fact]
    public void ContainsRenderTarget_PlainTree_ReturnsFalse()
    {
        var root = new ContainerRuntime();
        var child = new ContainerRuntime();
        root.Children.Add(child);
        child.Children.Add(new ContainerRuntime());

        GumRenderTargetBaker.ContainsRenderTarget(new[] { root }).ShouldBeFalse();
    }

    [Fact]
    public void ContainsRenderTarget_NestedRenderTargetContainer_ReturnsTrue()
    {
        var root = new ContainerRuntime();
        var child = new ContainerRuntime();
        root.Children.Add(child);
        child.Children.Add(new ContainerRuntime { IsRenderTarget = true });

        GumRenderTargetBaker.ContainsRenderTarget(new[] { root }).ShouldBeTrue();
    }

    [Fact]
    public void ContainsRenderTarget_RenderTargetRootItself_ReturnsTrue()
    {
        var root = new ContainerRuntime { IsRenderTarget = true };

        GumRenderTargetBaker.ContainsRenderTarget(new[] { root }).ShouldBeTrue();
    }

    // Gum bakes an invisible container when a visible sprite references it, so an invisible render target
    // must not be skipped on visibility alone.
    [Fact]
    public void ContainsRenderTarget_InvisibleRenderTargetContainer_ReturnsTrue()
    {
        var root = new ContainerRuntime();
        root.Children.Add(new ContainerRuntime { IsRenderTarget = true, Visible = false });

        GumRenderTargetBaker.ContainsRenderTarget(new[] { root }).ShouldBeTrue();
    }

    [Fact]
    public void ContainsRenderTarget_SpriteReferencingRenderTarget_ReturnsTrue()
    {
        var source = new ContainerRuntime { IsRenderTarget = true };
        var root = new ContainerRuntime();
        root.Children.Add(new SpriteRuntime { RenderTargetTextureSource = source });

        GumRenderTargetBaker.ContainsRenderTarget(new[] { root }).ShouldBeTrue();
    }

    [Fact]
    public void ContainsRenderTarget_NoRoots_ReturnsFalse()
    {
        GumRenderTargetBaker.ContainsRenderTarget(System.Array.Empty<Gum.Wireframe.GraphicalUiElement>()).ShouldBeFalse();
    }

    // Bake must not call Gum's PreRenderRoots at all for a scene with no render targets, and must
    // still call it (with the roots) when one is present.
    [Fact]
    public void Bake_NoRenderTargets_SkipsPreRender()
    {
        var screen = new TestScreen();
        var hud = new ContainerRuntime();
        hud.Children.Add(new ContainerRuntime());
        screen.Add(hud);
        int calls = 0;
        var baker = new GumRenderTargetBaker((_, _, _) => calls++);

        baker.Bake(screen.GumRenderablesToBake(screen.Cameras[0]), screen.Cameras[0]);

        calls.ShouldBe(0);
    }

    [Fact]
    public void Bake_WithRenderTarget_PreRendersTheRoots()
    {
        var screen = new TestScreen();
        var hud = new ContainerRuntime();
        hud.Children.Add(new ContainerRuntime { IsRenderTarget = true });
        screen.Add(hud);
        var bakedRoots = new System.Collections.Generic.List<Gum.Wireframe.GraphicalUiElement>();
        var baker = new GumRenderTargetBaker((_, _, roots) => bakedRoots.AddRange(roots));

        baker.Bake(screen.GumRenderablesToBake(screen.Cameras[0]), screen.Cameras[0]);

        bakedRoots.ShouldContain(hud);
    }
}
