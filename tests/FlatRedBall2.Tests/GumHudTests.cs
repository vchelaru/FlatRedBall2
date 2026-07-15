using System.Linq;
using FlatRedBall2.Rendering;
using Gum.Wireframe;
using Gum.GueDeriving;
using Shouldly;
using Xunit;
using FlatRedBall2;

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
    public void AddOverlay_ParentsVisualUnderOverlayRoot_WithoutPerVisualRenderable()
    {
        // Blob model (#659): AddOverlay just parents under OverlayRoot; the OverlayRoot itself is
        // registered once as a drawn overlay blob at activation, so no per-visual GumRenderable is
        // created — the whole root (and everything AddOverlay/AddToRoot put under it) draws together.
        var screen = new TestScreen();
        var visual = new ContainerRuntime();

        screen.AddOverlay(visual);

        screen.OverlayRoot.Children.ShouldContain(visual);
        screen.GumRenderables.ShouldNotContain(r => r.Visual == visual);
    }

    [Fact]
    public void Activation_UnifiesGumRootWithActiveScreenOverlayRoot()
    {
        // #659: element.AddToRoot() (-> GumService.Root) and screen.AddOverlay() (-> OverlayRoot)
        // must target the same container so AddToRoot content actually draws. On activation the
        // active screen's OverlayRoot becomes GumService.Root.
        var engine = new FlatRedBallService();
        engine.Start<TestScreen>();
        var screen = (TestScreen)engine.CurrentScreen;

        engine.Gum.Root.ShouldBeSameAs(screen.OverlayRoot);
    }

    // AddOverlayRoot wires up Gum Forms' global PopupRoot/ModalRoot (ComboBox dropdowns, MenuItem
    // submenus, ListBox popups) so they actually draw — see issue #656. Unlike AddOverlay, the
    // passed-in root must NOT be reparented under Screen.OverlayRoot: PopupRoot/ModalRoot are
    // independent top-level Gum roots that Gum's own Forms code expects to remain unparented.

    [Fact]
    public void AddOverlayRoot_RootRegisteredAsOverlay_NotParentedUnderOverlayRoot()
    {
        var screen = new TestScreen();
        var popupRoot = new ContainerRuntime();

        screen.AddOverlayRoot(popupRoot);

        screen.GumRenderables.ShouldContain(r => r.Visual == popupRoot);
        screen.OverlayRoot.Children.ShouldNotContain(popupRoot);
        var renderable = screen.GumRenderables.First(r => r.Visual == popupRoot);
        renderable.ShouldDrawForCamera(screen.Cameras[0]).ShouldBeFalse();
    }

    [Fact]
    public void AddOverlayRoot_RegisteredAfterOverlayRootBlob_DrawnOnTop()
    {
        // Draw order follows GumRenderables insertion order (see Screen.DrawOverlay). Under the blob
        // model the engine registers the OverlayRoot blob first, then popup roots, so popups draw
        // over overlay content. Assert the AddOverlayRoot ordering primitive the engine relies on.
        var screen = new TestScreen();
        var popupRoot = new ContainerRuntime();
        screen.AddOverlayRoot(screen.OverlayRoot);

        screen.AddOverlayRoot(popupRoot);

        var renderables = screen.GumRenderables.ToList();
        var overlayIndex = renderables.FindIndex(r => r.Visual == screen.OverlayRoot);
        var popupIndex = renderables.FindIndex(r => r.Visual == popupRoot);
        popupIndex.ShouldBeGreaterThan(overlayIndex);
    }

    // Engine-created Gum roots (Camera.UiRoot, Screen.OverlayRoot) are full-canvas-sized
    // ContainerRuntimes. ContainerRuntime's ctor sets HasEvents=true, which means the
    // cursor's hit-test treats the root itself as the target and steals clicks away from
    // any authored UI underneath. The roots are an implementation detail — they should be
    // input-transparent so children opt into events normally.

    [Fact]
    public void CameraUiRoot_HasEventsIsFalse_SoCursorPassesThroughToChildren()
    {
        var screen = new TestScreen();
        var uiRoot = (InteractiveGue)screen.Cameras[0].UiRoot;
        uiRoot.HasEvents.ShouldBeFalse();
    }

    [Fact]
    public void OverlayRoot_HasEventsIsFalse_SoCursorPassesThroughToChildren()
    {
        var screen = new TestScreen();
        var overlayRoot = (InteractiveGue)screen.OverlayRoot;
        overlayRoot.HasEvents.ShouldBeFalse();
    }

    // Setting Entity.IsVisible = false should hide every renderable the entity owns, including
    // entity-attached Gum visuals — matching how it hides Sprites and Shapes. The render loop
    // skips any IAttachable whose Parent.IsAbsoluteVisible is false, so verify the Gum
    // renderable's Parent points back to the entity (so that gate fires).

    [Fact]
    public void EntityAttachedGumVisual_RenderableParentedToEntity_HiddenWhenEntityIsInvisible()
    {
        var engine = new FlatRedBallService();
        engine.Start<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());
        var screen = (TestScreen)engine.CurrentScreen;
        var entity = new Entity();
        screen.Register(entity);
        var visual = new ContainerRuntime();
        entity.Add(visual);

        entity.IsVisible = false;

        var renderable = (IAttachable)screen.GumRenderables[0];
        var parent = renderable.Parent;
        parent.ShouldBe(entity);
        parent!.IsAbsoluteVisible.ShouldBeFalse();
    }

    // EntityVisualsRoot is the screen-level root that entity-attached Gum visuals are parented
    // under — the missing piece that makes them reachable from Gum's update tree (cursor input,
    // animation tick, hot-reload). Without parenting, Gum has no way to walk to them.

    [Fact]
    public void EntityAdd_GraphicalUiElement_VisualParentedToEntityVisualsRoot()
    {
        var engine = new FlatRedBallService();
        engine.Start<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());
        var screen = (TestScreen)engine.CurrentScreen;
        var entity = new Entity();
        screen.Register(entity);
        var visual = new ContainerRuntime();

        entity.Add(visual);

        screen.EntityVisualsRoot.Children.ShouldContain(visual);
    }

    [Fact]
    public void EntityRemove_GraphicalUiElement_VisualUnparentedFromEntityVisualsRoot()
    {
        var engine = new FlatRedBallService();
        engine.Start<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());
        var screen = (TestScreen)engine.CurrentScreen;
        var entity = new Entity();
        screen.Register(entity);
        var visual = new ContainerRuntime();
        entity.Add(visual);

        entity.Remove(visual);

        screen.EntityVisualsRoot.Children.ShouldNotContain(visual);
    }

    [Fact]
    public void EntityDestroy_UnparentsAllGumVisualsFromEntityVisualsRoot()
    {
        var engine = new FlatRedBallService();
        engine.Start<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());
        var screen = (TestScreen)engine.CurrentScreen;
        var entity = new Entity();
        screen.Register(entity);
        var a = new ContainerRuntime();
        var b = new ContainerRuntime();
        entity.Add(a);
        entity.Add(b);

        entity.Destroy();

        screen.EntityVisualsRoot.Children.ShouldNotContain(a);
        screen.EntityVisualsRoot.Children.ShouldNotContain(b);
    }

    [Fact]
    public void EntityVisualsRoot_HasEventsIsFalse_SoCursorPassesThroughToChildren()
    {
        // Same rationale as Camera.UiRoot and OverlayRoot: a full-canvas root with
        // HasEvents=true would steal cursor hit-tests from its children. The root is
        // engine bookkeeping; only the children should receive events.
        var screen = new TestScreen();
        var entityVisualsRoot = (InteractiveGue)screen.EntityVisualsRoot;
        entityVisualsRoot.HasEvents.ShouldBeFalse();
    }

}
