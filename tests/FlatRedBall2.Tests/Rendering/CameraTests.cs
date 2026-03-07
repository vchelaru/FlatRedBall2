using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

public class CameraTests
{
    private static Camera MakeCamera(int vpWidth, int vpHeight)
    {
        var camera = new Camera();
        camera.SetViewport(new Viewport(0, 0, vpWidth, vpHeight));
        return camera;
    }

    [Fact]
    public void Zoom_Default_IsOne()
    {
        var camera = new Camera();
        camera.Zoom.ShouldBe(1f);
    }

    [Fact]
    public void WorldToScreen_ZoomTwo_ScalesPositionCloserToCenter()
    {
        // At zoom=2, world units map to twice as many pixels — a point at (100,0) should be twice as far from center
        var camera = MakeCamera(1280, 720);
        camera.TargetWidth = 1280;
        camera.TargetHeight = 720;
        camera.Zoom = 2f;

        var screen = camera.WorldToScreen(new System.Numerics.Vector2(100f, 0f));
        // center is 640; at zoom=2, scale=2px/unit, so x = 640 + 100*2 = 840
        screen.X.ShouldBe(840f, tolerance: 0.01f);
    }

    [Fact]
    public void ScreenToWorld_ZoomTwo_InvertsWorldToScreen()
    {
        var camera = MakeCamera(1280, 720);
        camera.TargetWidth = 1280;
        camera.TargetHeight = 720;
        camera.Zoom = 2f;

        var world = new System.Numerics.Vector2(150f, -80f);
        var screen = camera.WorldToScreen(world);
        var roundtrip = camera.ScreenToWorld(screen);

        roundtrip.X.ShouldBe(world.X, tolerance: 0.01f);
        roundtrip.Y.ShouldBe(world.Y, tolerance: 0.01f);
    }
}

public class DisplaySettingsTests
{
    [Fact]
    public void ComputeDestinationViewport_NoFixedAspectRatio_ReturnsFullWindow()
    {
        var settings = new DisplaySettings();

        var vp = settings.ComputeDestinationViewport(1920, 1080);

        vp.X.ShouldBe(0);
        vp.Y.ShouldBe(0);
        vp.Width.ShouldBe(1920);
        vp.Height.ShouldBe(1080);
    }

    [Fact]
    public void ComputeDestinationViewport_WiderWindow_Pillarboxes()
    {
        // 16:9 target in a 21:9 window — bars on left and right
        var settings = new DisplaySettings { FixedAspectRatio = 16f / 9f };

        var vp = settings.ComputeDestinationViewport(2560, 1080);

        vp.Height.ShouldBe(1080);
        vp.Width.ShouldBe((int)(1080 * 16f / 9f)); // 1920
        vp.Y.ShouldBe(0);
        vp.X.ShouldBe((2560 - vp.Width) / 2); // centered
    }

    [Fact]
    public void ComputeDestinationViewport_TallerWindow_Letterboxes()
    {
        // 16:9 target in a 4:3 window — bars on top and bottom
        var settings = new DisplaySettings { FixedAspectRatio = 16f / 9f };

        var vp = settings.ComputeDestinationViewport(1024, 768);

        vp.Width.ShouldBe(1024);
        vp.Height.ShouldBe((int)(1024 / (16f / 9f))); // 576
        vp.X.ShouldBe(0);
        vp.Y.ShouldBe((768 - vp.Height) / 2); // centered
    }
}
