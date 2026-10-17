using System;
using System.Numerics;
using FlatRedBall2.Automation;
using FlatRedBall2.Rendering;
using FlatRedBall2.UI;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.UI;

// Issue #664: Gum hit-tests every root through ONE shared RenderingLibrary.Camera, which at input
// time holds whatever the previous frame's last draw pass left. GumInputRouting.Apply gives each
// root its own window-pixel -> canvas mapping so a control is clickable where it is drawn,
// whichever camera (or overlay) it was drawn under.
public class GumInputRoutingTests
{
    private const int WindowW = 1280;
    private const int WindowH = 720;

    private class TestScreen : Screen { }

    private static (TestScreen Screen, FixedCameraManagers Managers) NewScreen(params (float X, float Y, float W, float H, float Zoom)[] cameras)
    {
        var screen = new TestScreen();
        var managers = new FixedCameraManagers();
        for (int i = 0; i < cameras.Length; i++)
        {
            var c = i == 0 ? screen.Cameras[0] : new Camera();
            if (i > 0) screen.Cameras.Add(c);
            c.Zoom = cameras[i].Zoom;
            c.NormalizedViewport = new NormalizedRectangle(cameras[i].X, cameras[i].Y, cameras[i].W, cameras[i].H);
            c.ApplyToHostRect(new Viewport(0, 0, WindowW, WindowH), WindowH);
        }
        screen.AttachManagers(managers);
        return (screen, managers);
    }

    private static ContainerRuntime Box(float x, float y) =>
        new ContainerRuntime { X = x, Y = y, Width = 100, Height = 40 };

    /// <summary>Runs the routing for a cursor at <paramref name="px"/> and asks Gum whether it is over <paramref name="visual"/>.</summary>
    private static bool IsOver(Screen screen, FixedCameraManagers managers, Vector2 px, GraphicalUiElement visual, InteractiveGue? pushed = null)
    {
        var frbCursor = new FlatRedBall2.Input.Cursor();
        frbCursor.Inject((int)px.X, (int)px.Y, primary: false, secondary: false);
        frbCursor.Update(TimeSpan.Zero);
        var cursor = new AutomationGumCursor(frbCursor);
        cursor.Activity(0);
        GumInputRouting.Apply(screen, px, managers.GumCamera, WindowW, WindowH, pushed);
        return ((InteractiveGue)visual).HasCursorOver(cursor);
    }

    /// <summary>Window pixel at the center of <paramref name="box"/> drawn under a camera whose canvas->pixel scale is <paramref name="scale"/>.</summary>
    private static Vector2 DrawnCenter(Camera camera, ContainerRuntime box, float scale) =>
        new(camera.Viewport.X + (box.X + box.Width / 2) * scale, camera.Viewport.Y + (box.Y + box.Height / 2) * scale);

    [Fact]
    public void UiRoot_ZoomedInsetCamera_CursorOverDrawnControl_IsOver()
    {
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var inset = screen.Cameras[1];
        var box = Box(20, 20);
        inset.Add(box);

        IsOver(screen, managers, DrawnCenter(inset, box, inset.PixelsPerUnit), box).ShouldBeTrue();
    }

    [Fact]
    public void UiRoot_ZoomedInsetCamera_CursorAtWindowTopLeft_IsNotOver()
    {
        // The issue's symptom: the inset control reacting to a click near the window's top-left, as
        // if the inset's canvas origin sat at the window origin at 1x.
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var box = Box(20, 20);
        screen.Cameras[1].Add(box);

        IsOver(screen, managers, new Vector2(70, 40), box).ShouldBeFalse();
    }

    [Fact]
    public void UiRoot_MainCameraBeneathInset_CursorOverMainControl_IsOverMainNotInset()
    {
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var mainBox = Box(20, 20);
        var insetBox = Box(20, 20);
        screen.Cameras[0].Add(mainBox);
        screen.Cameras[1].Add(insetBox);
        var click = DrawnCenter(screen.Cameras[0], mainBox, screen.Cameras[0].PixelsPerUnit);

        IsOver(screen, managers, click, mainBox).ShouldBeTrue();
        IsOver(screen, managers, click, insetBox).ShouldBeFalse();
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void UiRoot_SplitScreenRightHalf_CursorOverDrawnControl_IsOver(float zoom)
    {
        var (screen, managers) = NewScreen((0, 0, 0.5f, 1, zoom), (0.5f, 0, 0.5f, 1, zoom));
        var right = screen.Cameras[1];
        var box = Box(20, 20);
        right.Add(box);

        IsOver(screen, managers, DrawnCenter(right, box, right.PixelsPerUnit), box).ShouldBeTrue();
    }

    [Fact]
    public void UiRoot_SplitScreenLeftHalf_CursorOverDrawnControl_IsOver()
    {
        // Before the fix the shared Gum camera held the LAST drawn camera (right half), so the
        // left camera's control was the one that missed.
        var (screen, managers) = NewScreen((0, 0, 0.5f, 1, 2f), (0.5f, 0, 0.5f, 1, 2f));
        var left = screen.Cameras[0];
        var box = Box(20, 20);
        left.Add(box);

        IsOver(screen, managers, DrawnCenter(left, box, left.PixelsPerUnit), box).ShouldBeTrue();
    }

    [Fact]
    public void UiRoot_ControlOverflowingViewport_CursorInNeighborViewport_IsNotOver()
    {
        // A control spilling past its camera's viewport edge is clipped from view, so the strip of
        // the neighboring camera it spills over must not respond to the click.
        var (screen, managers) = NewScreen((0, 0, 0.5f, 1, 1f), (0.5f, 0, 0.5f, 1, 1f));
        var left = screen.Cameras[0];
        var spill = new ContainerRuntime { X = 600, Y = 20, Width = 200, Height = 40 }; // viewport is 640 wide
        left.Add(spill);

        IsOver(screen, managers, new Vector2(700, 40), spill).ShouldBeFalse();
    }

    [Fact]
    public void ScreenSpaceRoot_ZoomedCamera_CursorOverDrawnControl_IsOver()
    {
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1.5f));
        var camera = screen.Cameras[0];
        camera.ApplyToHostRect(new Viewport(0, 0, WindowW, WindowH), WindowH / 2); // window is 2x the design height
        var layer = new FlatRedBall2.Rendering.Layer("SS") { IsScreenSpace = true };
        var box = Box(20, 80);
        camera.Add(box, layer);
        // ScreenSpaceRoot ignores Camera.Zoom: scale is window height / design height.
        float scale = camera.Viewport.Height / (float)camera.OrthogonalHeight;

        IsOver(screen, managers, DrawnCenter(camera, box, scale), box).ShouldBeTrue();
    }

    [Fact]
    public void PopupRoot_ZoomedInsetCamera_CursorOverDrawnPopupChild_IsOver()
    {
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var inset = screen.Cameras[1];
        var box = Box(20, 20);
        inset.PopupRoot.Children.Add(box);

        IsOver(screen, managers, DrawnCenter(inset, box, inset.PixelsPerUnit), box).ShouldBeTrue();
    }

    [Fact]
    public void ModalRoot_ZoomedInsetCamera_CursorOverDrawnModalChild_IsOver()
    {
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var inset = screen.Cameras[1];
        var box = Box(20, 20);
        inset.ModalRoot.Children.Add(box);

        IsOver(screen, managers, DrawnCenter(inset, box, inset.PixelsPerUnit), box).ShouldBeTrue();
    }

    [Fact]
    public void OverlayRoot_AfterZoomedCameraLeftGumCameraZoomed_CursorOverControl_IsOver()
    {
        // Overlay content is drawn 1:1 in window pixels. The shared Gum camera was left at an inset
        // camera's zoom/client rect by the previous frame's last camera pass.
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        managers.GumCamera.Zoom = 0.9f;
        managers.GumCamera.ClientWidth = 512;
        managers.GumCamera.ClientLeft = 742;
        var box = Box(300, 520);
        screen.OverlayRoot.Children.Add(box);

        IsOver(screen, managers, new Vector2(350, 540), box).ShouldBeTrue();
    }

    [Fact]
    public void OverlayRoot_CursorOverZoomedInsetCamera_ControlIsOverAtItsWindowPixels()
    {
        // Overlay content is laid out in window pixels. With the shared Gum camera on the inset's
        // mapping (cursor is over the inset), the overlay still has to resolve 1:1.
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var box = Box(800, 100);
        screen.OverlayRoot.Children.Add(box);

        IsOver(screen, managers, new Vector2(850, 120), box).ShouldBeTrue();
        IsOver(screen, managers, new Vector2(900, 300), box).ShouldBeFalse();
    }

    [Fact]
    public void EntityVisual_DrawnLastUnderOtherCamera_CursorInFirstCamera_IsOverAtFirstCamerasDrawnPosition()
    {
        // An entity visual is drawn once per camera and its canvas position is rewritten on every
        // pass, so at input time it holds the LAST camera's position. A click in the first camera
        // must be resolved against the first camera's projection of the entity.
        var (screen, managers) = NewScreen((0, 0, 0.5f, 1, 2f), (0.5f, 0, 0.5f, 1, 2f));
        screen.Cameras[1].X = 300f; // right camera looks elsewhere, so the two projections differ
        var entity = new Entity { X = 150f, Y = -100f };
        var visual = Box(0, 0);
        screen.AddGumForEntity(visual, entity, null);
        var renderable = screen.GumRenderables[^1];
        var first = screen.Cameras[0];
        renderable.PositionForCamera(screen.Cameras[1]); // what the last draw pass left behind

        // Where the first camera draws the entity: world -> viewport pixels (+ viewport origin).
        var p = first.WorldToScreen(new Vector2(150f, -100f));
        // Visual origin is top-left, so aim at the box center offset by half its size in pixels.
        var click = new Vector2(first.Viewport.X + p.X + 50 * first.PixelsPerUnit, first.Viewport.Y + p.Y + 20 * first.PixelsPerUnit);

        IsOver(screen, managers, click, visual).ShouldBeTrue();
    }

    // Gum's drag/caret math (Slider, ScrollBar, TextBox) is (cursor.X - GraphicsDevice.Viewport.X) /
    // Renderer.Camera.Zoom, read straight off the shared camera and the device viewport. Apply must
    // leave both on the mapping of the camera the gesture belongs to, and return that viewport.
    private static float GumCanvasX(Viewport deviceViewport, FixedCameraManagers managers, float pixelX) =>
        (pixelX - deviceViewport.X) / managers.GumCamera.Zoom;

    [Fact]
    public void Apply_CursorOverZoomedInset_LeavesGumCameraAndViewportOnTheInsetMapping()
    {
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var inset = screen.Cameras[1];
        float pixelX = 900f;

        var viewport = GumInputRouting.Apply(screen, new Vector2(pixelX, 100), managers.GumCamera, WindowW, WindowH);

        GumCanvasX(viewport, managers, pixelX).ShouldBe((pixelX - inset.Viewport.X) / inset.PixelsPerUnit, 0.001f);
    }

    [Fact]
    public void Apply_PushedControlOnInset_CursorDraggedOutsideInset_KeepsInsetMapping()
    {
        // Dragging a Slider thumb past the inset's edge must keep reading the inset's canvas.
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var inset = screen.Cameras[1];
        var thumb = Box(20, 20);
        inset.Add(thumb);
        float pixelX = 400f; // well left of the inset, over the main camera

        var viewport = GumInputRouting.Apply(screen, new Vector2(pixelX, 100), managers.GumCamera, WindowW, WindowH, pushed: thumb);

        GumCanvasX(viewport, managers, pixelX).ShouldBe((pixelX - inset.Viewport.X) / inset.PixelsPerUnit, 0.001f);
    }

    [Fact]
    public void UiRoot_PushedControlDraggedOutsideViewport_IsStillMappedToItsCanvas()
    {
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f), (0.58f, 0.05f, 0.40f, 0.45f, 2f));
        var inset = screen.Cameras[1];
        var spill = new ContainerRuntime { X = 600, Y = 20, Width = 200, Height = 40 }; // inset canvas is ~568 wide
        inset.Add(spill);
        var outside = new Vector2(inset.Viewport.X + 650 * inset.PixelsPerUnit, inset.Viewport.Y + 40 * inset.PixelsPerUnit);
        outside.X.ShouldBeGreaterThan(inset.Viewport.X + inset.Viewport.Width);

        IsOver(screen, managers, outside, spill, pushed: spill).ShouldBeTrue();
        IsOver(screen, managers, outside, spill).ShouldBeFalse();
    }

    [Fact]
    public void BuildUpdateList_EveryCameraRoot_IncludingScreenSpaceRoot_IsReceivingInput()
    {
        // ScreenSpaceRoot controls (Camera.Add with an IsScreenSpace layer) were drawn but never
        // handed to Gum's update, so they ignored the cursor entirely.
        var (screen, _) = NewScreen((0, 0, 0.5f, 1, 1f), (0.5f, 0, 0.5f, 1, 1f));
        var roots = new System.Collections.Generic.List<GraphicalUiElement>();

        GumInputRouting.BuildUpdateList(screen, roots);

        foreach (var camera in screen.Cameras)
        {
            roots.ShouldContain(camera.UiRoot);
            roots.ShouldContain(camera.ScreenSpaceRoot);
            roots.ShouldContain(camera.PopupRoot);
            roots.ShouldContain(camera.ModalRoot);
            // Popup/modal outrank the HUD they sit above.
            roots.IndexOf(camera.PopupRoot).ShouldBeGreaterThan(roots.IndexOf(camera.UiRoot));
            roots.IndexOf(camera.ModalRoot).ShouldBeGreaterThan(roots.IndexOf(camera.PopupRoot));
        }
        roots.ShouldContain(screen.EntityVisualsRoot);
        roots[^1].ShouldBe(screen.OverlayRoot);
    }

    [Fact]
    public void Apply_ResetsGumCameraToNeutral_RegardlessOfPreviousDrawPass()
    {
        var (screen, managers) = NewScreen((0, 0, 1, 1, 1f));
        var gum = managers.GumCamera;
        gum.Zoom = 0.45f;
        gum.ClientWidth = 512;
        gum.ClientHeight = 324;
        gum.ClientLeft = 742;
        gum.ClientTop = 36;

        GumInputRouting.Apply(screen, new Vector2(10, 10), gum, WindowW, WindowH);

        gum.Zoom.ShouldBe(1f);
        gum.ClientWidth.ShouldBe(WindowW);
        gum.ClientHeight.ShouldBe(WindowH);
        gum.ClientLeft.ShouldBe(0);
        gum.ClientTop.ShouldBe(0);
    }
}
