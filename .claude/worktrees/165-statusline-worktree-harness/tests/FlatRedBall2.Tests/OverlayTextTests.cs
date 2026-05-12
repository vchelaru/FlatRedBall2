using FlatRedBall2.Diagnostics;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class OverlayTextTests
{
    [Fact]
    public void WorldToCanvas_OneToOneViewport_CenterMapsToCanvasCenter()
    {
        var camera = new Camera { OrthogonalWidth = 1280, OrthogonalHeight = 720 };
        var (cx, cy) = Overlay.WorldToCanvas(camera, camera.X, camera.Y);

        cx.ShouldBe(640f, tolerance: 0.001f);
        cy.ShouldBe(360f, tolerance: 0.001f);
    }

    [Fact]
    public void WorldToCanvas_UpscaledViewport_UsesDesignUnitsNotPixels()
    {
        // PlatformKing case: 426x240 design (orthogonal) rendered into a much larger window.
        // The canvas size is design-unit (426x240) regardless of viewport pixels — so the
        // label must land at (213, 120), not (640, 360).
        var camera = new Camera
        {
            OrthogonalWidth = 426,
            OrthogonalHeight = 240,
            X = 1234f,
            Y = -567f,
        };

        var (cx, cy) = Overlay.WorldToCanvas(camera, camera.X, camera.Y);

        cx.ShouldBe(213f, tolerance: 0.001f);
        cy.ShouldBe(120f, tolerance: 0.001f);
    }

    [Fact]
    public void WorldToCanvas_OffsetWorldPoint_FlipsY()
    {
        // World Y+ up; canvas Y+ down. A world point above camera center maps below canvas center.
        var camera = new Camera
        {
            OrthogonalWidth = 426,
            OrthogonalHeight = 240,
            X = 1000f,
            Y = 500f,
        };

        // World point 50 right and 30 above the camera center.
        var (cx, cy) = Overlay.WorldToCanvas(camera, 1050f, 530f);

        cx.ShouldBe(263f, tolerance: 0.001f); // 213 + 50
        cy.ShouldBe(90f, tolerance: 0.001f);  // 120 - 30 (Y flip)
    }
}
