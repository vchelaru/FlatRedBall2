using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Rendering;
using GumCamera = RenderingLibrary.Camera;

namespace FlatRedBall2.UI;

/// <summary>
/// Makes Gum hit-test each FlatRedBall2 root under the transform it was drawn with (issue #664).
/// </summary>
/// <remarks>
/// Gum maps the cursor into canvas units through ONE shared <c>Renderer.Camera</c>, which at input
/// time holds whatever the previous frame's last draw pass left behind — an inset camera's zoom and
/// client rect, or the overlay's. A control drawn under any other camera was therefore tested at the
/// wrong place. Instead of depending on that leftover state, <see cref="Apply"/> parks the shared
/// camera at an identity mapping and gives every root its own window-pixel → canvas mapping through
/// <c>GraphicalUiElement.HitTestTransformMatrix</c>, which descendants inherit. Roots whose camera's
/// viewport does not contain the cursor are mapped far off-canvas so they cannot catch it.
/// </remarks>
internal static class GumInputRouting
{
    // Far enough off any canvas that no control can be under it.
    private static readonly Matrix3x2 NoHit = Matrix3x2.CreateTranslation(-1e7f, -1e7f);

    /// <summary>
    /// Call once per frame, after input is polled and before Gum's update, with the cursor position
    /// in window pixels.
    /// </summary>
    internal static void Apply(Screen screen, Vector2 cursorScreenPosition, GumCamera gumCamera, int backBufferWidth, int backBufferHeight)
    {
        // Identity mapping: window pixel == canvas unit, the same state the 1:1 overlay pass leaves.
        gumCamera.Zoom = 1f;
        gumCamera.X = 0f;
        gumCamera.Y = 0f;
        gumCamera.ClientWidth = backBufferWidth;
        gumCamera.ClientHeight = backBufferHeight;
        gumCamera.ClientLeft = 0;
        gumCamera.ClientTop = 0;

        // Cameras draw in list order, so the last one containing the cursor is the one on top.
        Camera? cursorCamera = null;
        for (int i = 0; i < screen.Cameras.Count; i++)
        {
            var camera = screen.Cameras[i];
            bool inside = Contains(camera, cursorScreenPosition);
            if (inside)
                cursorCamera = camera;

            var canvas = inside ? PixelToCanvas(camera, GumRenderBatch.Instance.ResolveZoom(camera)) : NoHit;
            var screenSpace = inside ? PixelToCanvas(camera, GumRenderBatch.ScreenSpaceInstance.ResolveZoom(camera)) : NoHit;
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
            return;
        }

        var renderables = screen.GumRenderables;
        for (int i = 0; i < renderables.Count; i++)
            renderables[i].PositionForCamera(cursorCamera);
        screen.EntityVisualsRoot.HitTestTransformMatrix = PixelToCanvas(cursorCamera, GumRenderBatch.Instance.ResolveZoom(cursorCamera));
    }

    /// <summary>
    /// Fills <paramref name="roots"/> with every Gum root that takes input, lowest cursor priority
    /// first (later entries win on overlap, matching draw stacking).
    /// </summary>
    internal static void BuildUpdateList(Screen screen, List<Gum.Wireframe.GraphicalUiElement> roots)
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
}
