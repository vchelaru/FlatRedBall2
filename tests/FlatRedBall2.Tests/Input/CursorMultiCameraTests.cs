using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Input;

public class CursorMultiCameraTests
{
    // Host window is 1280x720; cameras use NormalizedViewport against this rect.
    private const int HostWidth = 1280;
    private const int HostHeight = 720;
    private const int OrthoHeight = 720;

    private static readonly Viewport Host = new Viewport(0, 0, HostWidth, HostHeight);

    private static MouseState MouseAt(int x, int y) =>
        new MouseState(
            x: x, y: y, scrollWheel: 0,
            leftButton: ButtonState.Released,
            middleButton: ButtonState.Released,
            rightButton: ButtonState.Released,
            xButton1: ButtonState.Released,
            xButton2: ButtonState.Released);

    private static Camera MakeCamera(float worldX, float worldY, NormalizedRectangle viewport)
    {
        var cam = new Camera { X = worldX, Y = worldY, NormalizedViewport = viewport };
        cam.ApplyToHostRect(Host, OrthoHeight);
        return cam;
    }

    [Fact]
    public void GetWorldPosition_ExplicitCamera_ProjectsThroughThatCameraIgnoringCursorLocation()
    {
        // P1 left half centered at world X=-1000, P2 right half centered at world X=1000.
        var p1 = MakeCamera(-1000f, 0f, new NormalizedRectangle(0f, 0f, 0.5f, 1f));
        var p2 = MakeCamera(1000f, 0f, new NormalizedRectangle(0.5f, 0f, 0.5f, 1f));
        var cursor = new Cursor();
        cursor.SetCameras(new List<Camera> { p1, p2 });

        // Cursor at center of P2's viewport (window x=960). Auto-pick is P2 → world (1000, 0).
        cursor.Update(MouseAt(960, 360), TimeSpan.Zero);
        cursor.WorldPosition.X.ShouldBe(1000f, tolerance: 1f);

        // Force projection through P1 — same window pixel, projected through P1's transform.
        // P1's viewport origin is (0, 0), so local = (960, 360); P1 is 640 wide, center 320,
        // so 960 lands 640 px right of P1's center → world X = -1000 + 640 = -360.
        var throughP1 = cursor.GetWorldPosition(p1);
        throughP1.X.ShouldBe(-360f, tolerance: 1f);
        throughP1.Y.ShouldBe(0f, tolerance: 1f);
    }

    [Fact]
    public void IsOver_SplitScreen_AutoPicksCorrectCamera()
    {
        var p1 = MakeCamera(-1000f, 0f, new NormalizedRectangle(0f, 0f, 0.5f, 1f));
        var p2 = MakeCamera(1000f, 0f, new NormalizedRectangle(0.5f, 0f, 0.5f, 1f));
        var cursor = new Cursor();
        cursor.SetCameras(new List<Camera> { p1, p2 });

        // Circle in P2's world space.
        var circle = new Circle { X = 1000f, Y = 0f, Radius = 10f };

        // Cursor at center of P2's viewport (window x=960) → world (1000, 0).
        cursor.Update(MouseAt(960, 360), TimeSpan.Zero);

        cursor.IsOver(circle).ShouldBeTrue();
    }

    [Fact]
    public void WorldPosition_CursorInLetterboxGap_StaysWithLastActiveCamera()
    {
        // P1 occupies 0..0.4, P2 occupies 0.6..1.0; gap from 0.4..0.6 (window x=512..768).
        var p1 = MakeCamera(-1000f, 0f, new NormalizedRectangle(0f, 0f, 0.4f, 1f));
        var p2 = MakeCamera(1000f, 0f, new NormalizedRectangle(0.6f, 0f, 0.4f, 1f));
        var cursor = new Cursor();
        cursor.SetCameras(new List<Camera> { p1, p2 });

        // Move into P1 first (window x within 0..512). Center of P1 is x=256.
        cursor.Update(MouseAt(256, 360), TimeSpan.Zero);
        cursor.WorldPosition.X.ShouldBe(-1000f, tolerance: 1f);

        // Move into the letterbox gap (x=640). Active camera is sticky, so projection still
        // routes through P1: same as calling GetWorldPosition(p1) directly.
        cursor.Update(MouseAt(640, 360), TimeSpan.Zero);
        cursor.WorldPosition.ShouldBe(cursor.GetWorldPosition(p1));
        // And it should NOT match P2's projection (proving stickiness).
        cursor.WorldPosition.ShouldNotBe(cursor.GetWorldPosition(p2));
    }

    [Fact]
    public void WorldPosition_NoCamerasRegistered_FallsBackToScreenPosition()
    {
        var cursor = new Cursor();
        cursor.Update(MouseAt(123, 456), TimeSpan.Zero);

        cursor.WorldPosition.ShouldBe(new Vector2(123f, 456f));
    }

    [Fact]
    public void WorldPosition_PartialViewport_AccountsForViewportOffset()
    {
        // Single camera occupying right half of window. Viewport center is window x=960.
        var cam = MakeCamera(0f, 0f, new NormalizedRectangle(0.5f, 0f, 0.5f, 1f));
        var cursor = new Cursor();
        cursor.SetCameras(new List<Camera> { cam });

        cursor.Update(MouseAt(960, 360), TimeSpan.Zero);

        cursor.WorldPosition.X.ShouldBe(0f, tolerance: 0.5f);
        cursor.WorldPosition.Y.ShouldBe(0f, tolerance: 0.5f);
    }

    [Fact]
    public void WorldPosition_SingleFullViewportCamera_CenterMaps_ToCameraXY()
    {
        var cam = MakeCamera(0f, 0f, NormalizedRectangle.FullViewport);
        var cursor = new Cursor();
        cursor.SetCameras(new List<Camera> { cam });

        cursor.Update(MouseAt(HostWidth / 2, HostHeight / 2), TimeSpan.Zero);

        cursor.WorldPosition.X.ShouldBe(0f, tolerance: 0.5f);
        cursor.WorldPosition.Y.ShouldBe(0f, tolerance: 0.5f);
    }

    [Fact]
    public void WorldPosition_SplitScreen_PicksCameraContainingCursor()
    {
        var p1 = MakeCamera(-1000f, 0f, new NormalizedRectangle(0f, 0f, 0.5f, 1f));
        var p2 = MakeCamera(1000f, 0f, new NormalizedRectangle(0.5f, 0f, 0.5f, 1f));
        var cursor = new Cursor();
        cursor.SetCameras(new List<Camera> { p1, p2 });

        // Cursor in P1 viewport center (x=320).
        cursor.Update(MouseAt(320, 360), TimeSpan.Zero);
        cursor.WorldPosition.X.ShouldBe(-1000f, tolerance: 1f);

        // Cursor in P2 viewport center (x=960).
        cursor.Update(MouseAt(960, 360), TimeSpan.Zero);
        cursor.WorldPosition.X.ShouldBe(1000f, tolerance: 1f);
    }
}
