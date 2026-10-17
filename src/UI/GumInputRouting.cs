using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Rendering;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Graphics;
using GumCamera = RenderingLibrary.Camera;

namespace FlatRedBall2.UI;

/// <summary>
/// Makes Gum hit-test and drag each FlatRedBall2 root under the transform it was drawn with (issue #664).
/// </summary>
/// <remarks>
/// Gum reads the cursor through ONE shared <c>Renderer.Camera</c> plus the device viewport, and at
/// input time both hold whatever the previous frame's last draw pass left behind. Two readers use
/// them: hit-testing (through <c>Camera.ScreenToWorld</c>) and the drag/caret math in Slider,
/// ScrollBar and TextBox (<c>(X - Viewport.X) / Zoom</c>). <see cref="Apply"/> sets both to the mapping
/// of the camera the current gesture belongs to, so drag math reads that camera's canvas, then gives
/// every root a <c>HitTestTransformMatrix</c> that cancels the shared camera and applies the root's
/// own pixel → canvas mapping, so hit-testing is right for every root regardless of which camera is
/// shared. Roots whose camera's viewport does not contain the cursor are mapped far off-canvas.
/// <para>
/// Overlapping cameras do not occlude each other: a lower camera's controls under an upper camera's
/// viewport stay clickable. Cameras are allowed a transparent <c>BackgroundColor</c> (a HUD camera
/// layered over the world), and nothing here can tell whether the pixels above a control are opaque,
/// so assuming occlusion would make that common layout unclickable. Priority among overlapping roots
/// is draw order, as before.
/// </para>
/// </remarks>
internal static class GumInputRouting
{
    // Far enough off any canvas that no control can be under it.
    private static readonly Matrix3x2 NoHit = Matrix3x2.CreateTranslation(-1e7f, -1e7f);

    /// <summary>
    /// Call once per frame, after input is polled and before Gum's update. Returns the viewport the
    /// caller must put on the graphics device for Gum's cursor math (Draw sets its own each pass).
    /// </summary>
    /// <param name="cursorScreenPosition">The cursor in window pixels.</param>
    /// <param name="pushed">The control the cursor is currently holding down, if any; its camera stays
    /// the mapping for the whole drag, even once the cursor leaves that camera's viewport.</param>
    /// <param name="globalPopupRoot">Gum's own unparented <c>PopupRoot</c>, laid out in window pixels like the overlay.</param>
    /// <param name="globalModalRoot">Gum's own unparented <c>ModalRoot</c>, laid out in window pixels like the overlay.</param>
    internal static Viewport Apply(Screen screen, Vector2 cursorScreenPosition, GumCamera gumCamera, int backBufferWidth, int backBufferHeight,
        InteractiveGue? pushed = null, GraphicalUiElement? globalPopupRoot = null, GraphicalUiElement? globalModalRoot = null)
    {
        // Cameras draw in list order, so the last one containing the cursor is the one on top.
        Camera? cursorCamera = null;
        for (int i = 0; i < screen.Cameras.Count; i++)
        {
            if (Contains(screen.Cameras[i], cursorScreenPosition))
                cursorCamera = screen.Cameras[i];
        }

        var (gestureCamera, gestureScreenSpace) = FindGesture(screen, pushed, cursorCamera);

        // The shared camera and device viewport take the gesture camera's mapping; with no camera
        // involved (overlay, or a cursor outside every viewport) they are the identity the overlay uses.
        var viewport = new Viewport(0, 0, backBufferWidth, backBufferHeight);
        float zoom = 1f;
        if (gestureCamera != null)
        {
            float scale = (gestureScreenSpace ? GumRenderBatch.ScreenSpaceInstance : GumRenderBatch.Instance).ResolveZoom(gestureCamera);
            if (scale > 0f)
            {
                viewport = gestureCamera.Viewport;
                zoom = scale;
            }
        }
        gumCamera.Zoom = zoom;
        gumCamera.X = 0f;
        gumCamera.Y = 0f;
        gumCamera.ClientWidth = viewport.Width;
        gumCamera.ClientHeight = viewport.Height;
        gumCamera.ClientLeft = viewport.X;
        gumCamera.ClientTop = viewport.Y;

        // Hit-testing maps raw pixels through the shared camera, so each root's matrix first undoes
        // that mapping (inverse of ScreenToWorld) and then applies the root's own pixel → canvas one.
        var cancelShared = InverseOfScreenToWorld(gumCamera);

        // Overlay content is laid out in window pixels: its mapping is "undo the shared camera" only.
        screen.OverlayRoot.HitTestTransformMatrix = cancelShared;
        if (globalPopupRoot != null)
            globalPopupRoot.HitTestTransformMatrix = cancelShared;
        if (globalModalRoot != null)
            globalModalRoot.HitTestTransformMatrix = cancelShared;

        for (int i = 0; i < screen.Cameras.Count; i++)
        {
            var camera = screen.Cameras[i];
            bool active = ReferenceEquals(camera, gestureCamera) || Contains(camera, cursorScreenPosition);

            var canvas = active ? PixelToCanvas(camera, GumRenderBatch.Instance.ResolveZoom(camera)) * cancelShared : NoHit;
            var screenSpace = active ? PixelToCanvas(camera, GumRenderBatch.ScreenSpaceInstance.ResolveZoom(camera)) * cancelShared : NoHit;
            camera.UiRoot.HitTestTransformMatrix = canvas;
            camera.PopupRoot.HitTestTransformMatrix = canvas;
            camera.ModalRoot.HitTestTransformMatrix = canvas;
            camera.ScreenSpaceRoot.HitTestTransformMatrix = screenSpace;
        }

        // Entity visuals are drawn under every camera and their canvas position is rewritten on each
        // pass, so it currently belongs to the last camera drawn. Re-project them for the camera
        // under the cursor so the click is resolved against what that camera shows.
        if (cursorCamera == null)
        {
            screen.EntityVisualsRoot.HitTestTransformMatrix = NoHit;
            return viewport;
        }

        var renderables = screen.GumRenderables;
        for (int i = 0; i < renderables.Count; i++)
            renderables[i].PositionForCamera(cursorCamera);
        screen.EntityVisualsRoot.HitTestTransformMatrix =
            PixelToCanvas(cursorCamera, GumRenderBatch.Instance.ResolveZoom(cursorCamera)) * cancelShared;
        return viewport;
    }

    /// <summary>
    /// Fills <paramref name="roots"/> with every Gum root that takes input, lowest cursor priority
    /// first (later entries win on overlap, matching draw stacking).
    /// </summary>
    internal static void BuildUpdateList(Screen screen, List<GraphicalUiElement> roots)
    {
        roots.Clear();
        // Entity-attached visuals lose to HUD elements and to the overlay on overlap.
        roots.Add(screen.EntityVisualsRoot);
        for (int i = 0; i < screen.Cameras.Count; i++)
        {
            var camera = screen.Cameras[i];
            roots.Add(camera.UiRoot);
            // Screen-space layers draw above the unlayered HUD, so they take the cursor first.
            roots.Add(camera.ScreenSpaceRoot);
            // Popup/modal after the HUD so an open popup/modal gets the cursor first, modal after popup.
            roots.Add(camera.PopupRoot);
            roots.Add(camera.ModalRoot);
        }
        roots.Add(screen.OverlayRoot);
    }

    /// <summary>
    /// The camera (and whether its screen-space scale applies) whose canvas Gum's drag math should read:
    /// the one owning the held control, else the camera under the cursor. None for overlay controls.
    /// </summary>
    private static (Camera? Camera, bool ScreenSpace) FindGesture(Screen screen, InteractiveGue? pushed, Camera? cursorCamera)
    {
        if (pushed == null)
            return (cursorCamera, false);

        GraphicalUiElement top = pushed;
        while (top.Parent is GraphicalUiElement parent)
            top = parent;

        if (ReferenceEquals(top, screen.EntityVisualsRoot))
            return (cursorCamera, false);
        for (int i = 0; i < screen.Cameras.Count; i++)
        {
            var camera = screen.Cameras[i];
            if (ReferenceEquals(top, camera.ScreenSpaceRoot))
                return (camera, true);
            if (ReferenceEquals(top, camera.UiRoot) || ReferenceEquals(top, camera.PopupRoot) || ReferenceEquals(top, camera.ModalRoot))
                return (camera, false);
        }
        return (null, false); // overlay or another unparented root: window pixels
    }

    private static bool Contains(Camera camera, Vector2 pixel)
    {
        var vp = camera.Viewport;
        return pixel.X >= vp.X && pixel.X < vp.X + vp.Width
            && pixel.Y >= vp.Y && pixel.Y < vp.Y + vp.Height;
    }

    /// <summary>Maps a window pixel to the canvas unit drawn there: viewport origin removed, then divided by the pixels-per-canvas-unit scale.</summary>
    private static Matrix3x2 PixelToCanvas(Camera camera, float pixelsPerCanvasUnit)
    {
        if (pixelsPerCanvasUnit <= 0f)
            return NoHit;
        var vp = camera.Viewport;
        return Matrix3x2.CreateTranslation(-vp.X, -vp.Y) * Matrix3x2.CreateScale(1f / pixelsPerCanvasUnit);
    }

    /// <summary>The inverse of what <c>ScreenToWorld</c> does with the camera's current state. It is affine, so three samples describe it.</summary>
    private static Matrix3x2 InverseOfScreenToWorld(GumCamera gumCamera)
    {
        gumCamera.ScreenToWorld(0, 0, out var x0, out var y0);
        gumCamera.ScreenToWorld(1, 0, out var x1, out var y1);
        gumCamera.ScreenToWorld(0, 1, out var x2, out var y2);
        var matrix = new Matrix3x2(x1 - x0, y1 - y0, x2 - x0, y2 - y0, x0, y0);
        return Matrix3x2.Invert(matrix, out var inverse) ? inverse : Matrix3x2.Identity;
    }
}
