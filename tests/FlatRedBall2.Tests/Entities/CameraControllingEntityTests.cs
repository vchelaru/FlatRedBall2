using FlatRedBall2.Entities;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Entities;

public class CameraControllingEntityTests
{
    // Creates a CameraControllingEntity with a camera already wired up (bypasses Factory/Engine).
    private static CameraControllingEntity MakeController(
        float entityX = 0f, float entityY = 0f,
        int targetWidth = 1280, int targetHeight = 720, float zoom = 1f)
    {
        var camera = new Camera();
        camera.SetViewport(new Viewport(0, 0, targetWidth, targetHeight));
        camera.TargetWidth  = targetWidth;
        camera.TargetHeight = targetHeight;
        camera.Zoom = zoom;

        var controller = new CameraControllingEntity();
        controller.X = entityX;
        controller.Y = entityY;
        controller.Camera = camera;
        return controller;
    }

    [Fact]
    public void GetTarget_NoTargets_ReturnsCameraEntityPosition()
    {
        var controller = MakeController(entityX: 150f, entityY: 75f);

        var result = controller.GetTarget();

        result.X.ShouldBe(150f);
        result.Y.ShouldBe(75f);
    }

    [Fact]
    public void GetTarget_TargetInsideDeadzone_CameraDoesNotMove()
    {
        var controller = MakeController(entityX: 0f, entityY: 0f);
        controller.ScrollingWindowWidth  = 200f;
        controller.ScrollingWindowHeight = 120f;

        // Target at (60, 40) — inside the 200×120 window centered at (0,0)
        var target = new Entity { X = 60f, Y = 40f };
        controller.Targets.Add(target);

        var result = controller.GetTarget();

        result.X.ShouldBe(0f);
        result.Y.ShouldBe(0f);
    }

    [Fact]
    public void GetTarget_TargetOutsideMapEdge_ClampsToMapBoundary()
    {
        // Camera shows 1280×720 (zoom=1, targetWidth=1280). Map is 2560×1440 centered at origin.
        // IsVisible half-width = 640. Max camera X = mapRight - visibleW/2 = 1280 - 640 = 640.
        var controller = MakeController(entityX: 0f, entityY: 0f);
        controller.Map = new BoundsRectangle(2560f, 1440f);

        var target = new Entity { X = 1500f, Y = 0f }; // beyond right map edge
        controller.Targets.Add(target);

        var result = controller.GetTarget();

        float expectedMaxX = 640f; // 1280 - 640
        result.X.ShouldBe(expectedMaxX, tolerance: 0.01f);
    }

    [Fact]
    public void GetTarget_CameraLargerThanMap_CentersOnMap()
    {
        // Zoom out so camera shows far more world than the map — should center on map.
        var controller = MakeController(entityX: 999f, entityY: 999f, zoom: 0.1f);
        // Map is 100×100 centered at origin. Camera at zoom=0.1 shows 12800×7200 world units.
        controller.Map = new BoundsRectangle(100f, 100f);

        var target = new Entity { X = 0f, Y = 0f };
        controller.Targets.Add(target);

        var result = controller.GetTarget();

        // Map center is (0,0); camera must center on map when it can't fit.
        result.X.ShouldBe(0f, tolerance: 0.01f);
        result.Y.ShouldBe(0f, tolerance: 0.01f);
    }
}
