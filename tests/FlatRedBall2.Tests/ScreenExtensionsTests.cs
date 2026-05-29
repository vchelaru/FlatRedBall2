using MonoGameGum.GueDeriving;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class ScreenExtensionsTests
{
    private class TestScreen : Screen { }

    [Fact]
    public void AddCameraUi_GraphicalUiElement_ParentsToPrimaryCameraUiRoot()
    {
        var screen = new TestScreen();
        var visual = new ContainerRuntime();

        screen.AddCameraUi(visual);

        screen.Cameras[0].UiRoot.Children.ShouldContain(visual);
        screen.OverlayRoot.Children.ShouldNotContain(visual);
    }

    [Fact]
    public void AddScreenOverlay_GraphicalUiElement_ParentsToOverlayRoot()
    {
        var screen = new TestScreen();
        var visual = new ContainerRuntime();

        screen.AddScreenOverlay(visual);

        screen.OverlayRoot.Children.ShouldContain(visual);
        screen.Cameras[0].UiRoot.Children.ShouldNotContain(visual);
    }
}
