using FlatRedBall2.Rendering;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;
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
        renderable.Parent.ShouldBe(entity);
        renderable.Parent.IsAbsoluteVisible.ShouldBeFalse();
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
