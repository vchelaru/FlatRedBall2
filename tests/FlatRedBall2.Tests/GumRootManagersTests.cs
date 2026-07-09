using FlatRedBall2.Rendering;
using Gum.Wireframe;
using Gum.GueDeriving;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

// Issue #657 / #659: FlatRedBall2's own Gum roots (Camera.UiRoot, Screen.OverlayRoot,
// Screen.EntityVisualsRoot, and the per-camera popup/modal roots) must have a SystemManagers
// attached so that EffectiveManagers resolves for everything parented under them. Gum gates
// several behaviors on EffectiveManagers != null (FrameworkElement.Loaded firing, TextBox
// hover-highlight, ScreenPixel corner-radius zoom compensation, popup close-on-outside-click),
// all of which silently no-op while EffectiveManagers stays null.
//
// The real engine attaches RenderingLibrary.SystemManagers.Default, which is only non-null once
// a GraphicsDevice-backed GumService.Initialize has run (null in this headless host — see
// GumShapeRuntimeTests). The testable core is the attach wiring itself: given any ISystemManagers,
// Screen.AttachManagers must make every root resolve it. The Draw/Initialize call site that passes
// the real Default is the thin untested wiring.
public class GumRootManagersTests
{
    private class TestScreen : Screen { }

    // ISystemManagers is a 3-member interface; EffectiveManagers only reads identity, never
    // dereferences Renderer, so a null-returning stub is sufficient.
    private sealed class StubSystemManagers : ISystemManagers
    {
        public bool EnableTouchEvents { get; set; }
        public IRenderer Renderer => null!;
        public void InvalidateSurface() { }
    }

    [Fact]
    public void AttachManagers_OverlayRoot_EffectiveManagersResolves()
    {
        var screen = new TestScreen();
        var managers = new StubSystemManagers();

        screen.AttachManagers(managers);

        screen.OverlayRoot.EffectiveManagers.ShouldBe(managers);
    }

    [Fact]
    public void AttachManagers_EntityVisualsRoot_EffectiveManagersResolves()
    {
        var screen = new TestScreen();
        var managers = new StubSystemManagers();

        screen.AttachManagers(managers);

        screen.EntityVisualsRoot.EffectiveManagers.ShouldBe(managers);
    }

    [Fact]
    public void AttachManagers_EveryCameraUiRoot_EffectiveManagersResolves()
    {
        var screen = new TestScreen();
        var second = new FlatRedBall2.Rendering.Camera();
        screen.Cameras.Add(second);
        var managers = new StubSystemManagers();

        screen.AttachManagers(managers);

        screen.Cameras[0].UiRoot.EffectiveManagers.ShouldBe(managers);
        second.UiRoot.EffectiveManagers.ShouldBe(managers);
    }

    [Fact]
    public void AttachManagers_ChildAddedUnderUiRoot_ResolvesManagersThroughParentChain()
    {
        // The whole point: a control the game adds resolves EffectiveManagers via its root ancestor.
        var screen = new TestScreen();
        var managers = new StubSystemManagers();
        screen.AttachManagers(managers);
        var control = new ContainerRuntime();

        screen.Add(control);

        control.EffectiveManagers.ShouldBe(managers);
    }
}
