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
    public void NormalizedViewport_Default_IsFullHostRect()
    {
        var camera = new Camera();

        camera.NormalizedViewport.ShouldBe(new NormalizedRectangle(0f, 0f, 1f, 1f));
    }

    [Fact]
    public void NormalizedViewport_HalfWidthRightHalf_PixelViewportIsRightHalfOfHostRect()
    {
        // Right-half split for player 2: normalized (0.5, 0, 0.5, 1) inside a 1280x720 host rect at (0,0).
        var camera = new Camera { NormalizedViewport = new NormalizedRectangle(0.5f, 0f, 0.5f, 1f) };

        camera.ApplyToHostRect(new Viewport(0, 0, 1280, 720), orthogonalHeight: 720);

        camera.Viewport.X.ShouldBe(640);
        camera.Viewport.Y.ShouldBe(0);
        camera.Viewport.Width.ShouldBe(640);
        camera.Viewport.Height.ShouldBe(720);
    }

    [Fact]
    public void ApplyToHostRect_HalfWidthViewport_DerivesOrthogonalWidthFromPixelAspect()
    {
        // Half-width viewport at design height 720 -> pixel 640x720 -> aspect 640/720 -> orthoW = 720 * (640/720) = 640.
        var camera = new Camera { NormalizedViewport = new NormalizedRectangle(0f, 0f, 0.5f, 1f) };

        camera.ApplyToHostRect(new Viewport(0, 0, 1280, 720), orthogonalHeight: 720);

        camera.OrthogonalHeight.ShouldBe(720);
        camera.OrthogonalWidth.ShouldBe(640);
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
        camera.OrthogonalWidth = 1280;
        camera.OrthogonalHeight = 720;
        camera.Zoom = 2f;

        var screen = camera.WorldToScreen(new System.Numerics.Vector2(100f, 0f));
        // center is 640; at zoom=2, scale=2px/unit, so x = 640 + 100*2 = 840
        screen.X.ShouldBe(840f, tolerance: 0.01f);
    }

    [Fact]
    public void ScreenToWorld_ZoomTwo_InvertsWorldToScreen()
    {
        var camera = MakeCamera(1280, 720);
        camera.OrthogonalWidth = 1280;
        camera.OrthogonalHeight = 720;
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
    public void AspectPolicy_Default_IsLocked()
    {
        // Locked-to-design-ratio is the safe default — no pixel distortion, no surprise extra world
        // visible when the window aspect differs from the design.
        var settings = new DisplaySettings();

        settings.AspectPolicy.ShouldBe(AspectPolicy.Locked);
    }

    [Fact]
    public void AllowUserResizing_Default_IsFalse()
    {
        // Fixed-canvas pattern is the safe default; resizing is opt-in.
        var settings = new DisplaySettings();

        settings.AllowUserResizing.ShouldBeFalse();
    }

    [Fact]
    public void ComputeDestinationViewport_LockedNullFixedRatio_DerivesFromResolution()
    {
        // Locked + null FixedAspectRatio uses ResolutionWidth/Height as the implied aspect.
        // 240x320 design (0.75) inside a 1000x800 window (1.25) — pillarbox to 0.75.
        var settings = new DisplaySettings
        {
            AspectPolicy = AspectPolicy.Locked,
            FixedAspectRatio = null,
            ResolutionWidth = 240,
            ResolutionHeight = 320,
        };

        var vp = settings.ComputeDestinationViewport(1000, 800);

        // height-bound: vp height = window height; vp width = height * 0.75 = 600
        vp.Height.ShouldBe(800);
        vp.Width.ShouldBe(600);
        vp.X.ShouldBe((1000 - 600) / 2);
        vp.Y.ShouldBe(0);
    }

    [Fact]
    public void ComputeDestinationViewport_LockedExplicitFixedRatio_PillarboxesToThatRatio()
    {
        // Explicit FixedAspectRatio overrides the resolution-derived aspect.
        // 16:9 target in a 21:9 window — bars on left and right
        var settings = new DisplaySettings { AspectPolicy = AspectPolicy.Locked, FixedAspectRatio = 16f / 9f };

        var vp = settings.ComputeDestinationViewport(2560, 1080);

        vp.Height.ShouldBe(1080);
        vp.Width.ShouldBe((int)(1080 * 16f / 9f)); // 1920
        vp.Y.ShouldBe(0);
        vp.X.ShouldBe((2560 - vp.Width) / 2);
    }

    [Fact]
    public void ComputeDestinationViewport_LockedTallerWindow_Letterboxes()
    {
        // 16:9 target in a 4:3 window — bars on top and bottom
        var settings = new DisplaySettings { AspectPolicy = AspectPolicy.Locked, FixedAspectRatio = 16f / 9f };

        var vp = settings.ComputeDestinationViewport(1024, 768);

        vp.Width.ShouldBe(1024);
        vp.Height.ShouldBe((int)(1024 / (16f / 9f))); // 576
        vp.X.ShouldBe(0);
        vp.Y.ShouldBe((768 - vp.Height) / 2);
    }

    [Fact]
    public void ComputeDestinationViewport_FreePolicy_FillsWindow()
    {
        // Free policy = no bars, viewport fills window regardless of aspect mismatch.
        var settings = new DisplaySettings { AspectPolicy = AspectPolicy.Free };

        var vp = settings.ComputeDestinationViewport(1920, 1080);

        vp.X.ShouldBe(0);
        vp.Y.ShouldBe(0);
        vp.Width.ShouldBe(1920);
        vp.Height.ShouldBe(1080);
    }
}
