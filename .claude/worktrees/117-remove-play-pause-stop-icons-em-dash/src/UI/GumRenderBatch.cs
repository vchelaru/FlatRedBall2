using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using Gum.Wireframe;
using NativeGumBatch = RenderingLibrary.Graphics.GumBatch;

namespace FlatRedBall2.UI;

/// <summary>
/// <see cref="IRenderBatch"/> implementation for Gum UI elements. Wraps Gum's own
/// <c>RenderingLibrary.Graphics.GumBatch</c> so that Gum draws can be interleaved with
/// world-space game objects via the Screen's Layer/Z sort.
/// </summary>
public class GumRenderBatch : IRenderBatch
{
    /// <summary>Singleton — every <see cref="GumRenderable"/> shares this batch.</summary>
    public static readonly GumRenderBatch Instance = new GumRenderBatch();

    private NativeGumBatch? _inner;

    /// <summary>
    /// Creates the inner <c>RenderingLibrary.Graphics.GumBatch</c>.
    /// Must be called after the engine's <c>GumService</c> has been initialized.
    /// Called automatically by <see cref="FlatRedBallService.Initialize"/>.
    /// </summary>
    internal void Initialize()
    {
        _inner = new NativeGumBatch();
    }

    /// <inheritdoc/>
    public bool FlipsY => false; // Gum renders in screen space; no Y-flip transform applied

    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
    {
        // PixelsPerUnit folds together window-vs-resolution scale AND the FlatRedBall Camera.Zoom.
        // We drive Gum rendering and Gum hit-testing from a single source — Renderer.Camera.Zoom —
        // which Gum's GetZoomAndMatrix bakes into basicEffect.View, and which
        // Cursor.XRespectingGumZoomAndBounds reads directly when converting window pixels into
        // canvas units. Pass null to GumBatch.Begin so we don't double-apply the scale on top
        // of Camera.Zoom.
        RenderingLibrary.SystemManagers.Default.Renderer.Camera.Zoom = camera.PixelsPerUnit;
        _inner!.Begin(null);
    }

    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch)
    {
        _inner!.End();
    }

    /// <summary>Draws a Gum element within an active Begin/End block.</summary>
    internal void DrawElement(GraphicalUiElement element)
    {
        _inner!.Draw(element);
    }
}
