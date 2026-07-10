using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Rendering;
using Gum.Wireframe;
using Gum.GueDeriving;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using Shouldly;
using Xunit;
using Camera = FlatRedBall2.Rendering.Camera;

namespace FlatRedBall2.Tests;

// Issue #659, Piece 2: per-camera popup/modal roots. A Forms popup (ComboBox dropdown, MenuItem
// submenu) opened from a control on a given camera must ROUTE to that camera's PopupRoot/ModalRoot
// (via GraphicalUiElement.ResolvePopupRoots), so it draws in that camera's pass at that camera's
// zoom. Where the popup ends up PARENTED and which camera draws it is FRB2's concern and is tested
// here. How Gum then POSITIONS it (edge-clamping) is Gum's concern — tracked in vchelaru/Gum#3591,
// not asserted here.
public class GumPopupRootTests
{
    private class TestScreen : Screen { }

    private sealed class StubSystemManagers : ISystemManagers
    {
        public bool EnableTouchEvents { get; set; }
        public IRenderer Renderer => null!;
        public void InvalidateSurface() { }
    }

    private static IReadOnlyList<Camera> Cams(Screen screen) => (IReadOnlyList<Camera>)screen.Cameras;

    [Fact]
    public void Camera_PopupAndModalRoots_AreDistinct_InputTransparent()
    {
        var camera = new Camera();

        camera.PopupRoot.ShouldNotBeSameAs(camera.ModalRoot);
        // HasEvents=false: like UiRoot, these full-canvas roots must not steal cursor hit-tests
        // from the popup content parented under them.
        ((InteractiveGue)camera.PopupRoot).HasEvents.ShouldBeFalse();
        ((InteractiveGue)camera.ModalRoot).HasEvents.ShouldBeFalse();
    }

    [Fact]
    public void AttachManagers_CameraPopupAndModalRoots_EffectiveManagersResolve()
    {
        var screen = new TestScreen();
        var managers = new StubSystemManagers();

        screen.AttachManagers(managers);

        screen.Cameras[0].PopupRoot.EffectiveManagers.ShouldBe(managers);
        screen.Cameras[0].ModalRoot.EffectiveManagers.ShouldBe(managers);
    }

    [Fact]
    public void ResolvePopupRootsFor_ControlUnderCameraUiRoot_ReturnsThatCamerasPair()
    {
        var screen = new TestScreen();
        var cam2 = new Camera();
        screen.Cameras.Add(cam2);
        var control = new ContainerRuntime();
        cam2.UiRoot.Children.Add(control);
        var globalPopup = new ContainerRuntime();
        var globalModal = new ContainerRuntime();

        var (popup, modal) = FlatRedBallService.ResolvePopupRootsFor(control, Cams(screen), globalPopup, globalModal);

        popup.ShouldBeSameAs(cam2.PopupRoot);
        modal.ShouldBeSameAs(cam2.ModalRoot);
    }

    [Fact]
    public void ResolvePopupRootsFor_ControlUnderCameraPopupRoot_ReturnsSameCamerasPair()
    {
        // A control living inside an already-open popup (e.g. a nested ComboBox) resolves to the
        // same camera's pair, so the nested popup stays in that camera's pass.
        var screen = new TestScreen();
        var control = new ContainerRuntime();
        screen.Cameras[0].PopupRoot.Children.Add(control);
        var globalPopup = new ContainerRuntime();
        var globalModal = new ContainerRuntime();

        var (popup, modal) = FlatRedBallService.ResolvePopupRootsFor(control, Cams(screen), globalPopup, globalModal);

        popup.ShouldBeSameAs(screen.Cameras[0].PopupRoot);
        modal.ShouldBeSameAs(screen.Cameras[0].ModalRoot);
    }

    [Fact]
    public void ResolvePopupRootsFor_ControlNotUnderAnyCamera_ReturnsGlobalFallback()
    {
        // A control under the screen overlay (or any non-camera root) falls back to Gum's global
        // popup/modal pair — the full-window overlay pass, matching pre-#659 behavior.
        var screen = new TestScreen();
        var control = new ContainerRuntime();
        screen.OverlayRoot.Children.Add(control);
        var globalPopup = new ContainerRuntime();
        var globalModal = new ContainerRuntime();

        var (popup, modal) = FlatRedBallService.ResolvePopupRootsFor(control, Cams(screen), globalPopup, globalModal);

        popup.ShouldBeSameAs(globalPopup);
        modal.ShouldBeSameAs(globalModal);
    }

    [Fact]
    public void RegisterCameraPopupRoots_RootsDrawInOwningCameraPassOnly()
    {
        // The per-camera popup/modal roots must draw in their own camera's pass (inheriting its
        // zoom/viewport) and be skipped for every other camera — same owning-camera filter HUD uses.
        var screen = new TestScreen();
        var cam2 = new Camera();
        screen.Cameras.Add(cam2);

        screen.RegisterCameraPopupRoots(cam2);

        var popupRenderable = screen.GumRenderables.Single(r => r.Visual == cam2.PopupRoot);
        var modalRenderable = screen.GumRenderables.Single(r => r.Visual == cam2.ModalRoot);
        popupRenderable.ShouldDrawForCamera(cam2).ShouldBeTrue();
        popupRenderable.ShouldDrawForCamera(screen.Cameras[0]).ShouldBeFalse();
        modalRenderable.ShouldDrawForCamera(cam2).ShouldBeTrue();
        modalRenderable.ShouldDrawForCamera(screen.Cameras[0]).ShouldBeFalse();
    }

    [Fact]
    public void RegisterCameraPopupRoots_ModalDrawnAfterPopup_SoModalIsTopmost()
    {
        // Draw order follows insertion order; modal must register after popup so a modal dialog
        // draws over an open popup, matching Gum's "modal is always topmost" intent.
        var screen = new TestScreen();

        screen.RegisterCameraPopupRoots(screen.Cameras[0]);

        var renderables = screen.GumRenderables.ToList();
        var popupIndex = renderables.FindIndex(r => r.Visual == screen.Cameras[0].PopupRoot);
        var modalIndex = renderables.FindIndex(r => r.Visual == screen.Cameras[0].ModalRoot);
        modalIndex.ShouldBeGreaterThan(popupIndex);
    }
}
