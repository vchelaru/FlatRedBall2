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
    /// <summary>Singleton for normal per-camera HUD — zoom-coupled. Every <see cref="GumRenderable"/> on a non-screen-space <see cref="Layer"/> shares this batch.</summary>
    public static readonly GumRenderBatch Instance = new GumRenderBatch(usesCameraZoom: true);

    /// <summary>
    /// Singleton for HUD on a <see cref="Layer"/> with <see cref="Layer.IsScreenSpace"/> — applies the
    /// window-vs-design-resolution scale but ignores <see cref="Camera.Zoom"/> (issue #798).
    /// </summary>
    public static readonly GumRenderBatch ScreenSpaceInstance = new GumRenderBatch(usesCameraZoom: false);

    private readonly bool _usesCameraZoom;
    private NativeGumBatch? _inner;

    private GumRenderBatch(bool usesCameraZoom)
    {
        _usesCameraZoom = usesCameraZoom;
    }

    /// <summary>
    /// Creates the inner <c>RenderingLibrary.Graphics.GumBatch</c>.
    /// Must be called after the engine's <c>GumService</c> has been initialized.
    /// Called automatically by <see cref="FlatRedBallService.Initialize(Microsoft.Xna.Framework.Game, EngineInitSettings)"/>.
    /// </summary>
    internal void Initialize()
    {
        _inner = new NativeGumBatch();
    }

    /// <inheritdoc/>
    public bool FlipsY => false; // Gum renders in screen space; no Y-flip transform applied

    /// <summary>
    /// The Gum render-zoom this instance drives from <paramref name="camera"/>: <see cref="Camera.PixelsPerUnit"/>
    /// (window scale × <see cref="Camera.Zoom"/>) for <see cref="Instance"/>, or window scale alone
    /// (<c>Camera.Zoom</c> excluded) for <see cref="ScreenSpaceInstance"/>. Extracted from <see cref="Begin"/>
    /// so it's testable without a <c>GraphicsDevice</c>-backed <c>SystemManagers.Default</c>.
    /// </summary>
    internal float ResolveZoom(Camera camera) =>
        _usesCameraZoom ? camera.PixelsPerUnit : camera.Viewport.Height / (float)camera.OrthogonalHeight;

    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
    {
        // We drive Gum rendering and Gum hit-testing from a single source — Renderer.Camera —
        // which Gum's GetZoomAndMatrix bakes into basicEffect.View, and which
        // Cursor.XRespectingGumZoomAndBounds / Camera.ScreenToWorld read directly when converting
        // window pixels into canvas units (ScreenToWorld is what Gum's own cursor hit-testing
        // actually uses for FRB2-hosted UI). Pass null to GumBatch.Begin so we don't double-apply
        // the scale on top of what we just set.
        SyncCursorCamera(RenderingLibrary.SystemManagers.Default.Renderer.Camera, camera);
        _inner!.Begin(null);
    }

    /// <summary>
    /// Syncs <paramref name="gumCamera"/> (Gum's own <c>RenderingLibrary.Camera</c>, a single object
    /// shared by every <see cref="Begin"/> call in a frame) from this frame's FlatRedBall
    /// <paramref name="camera"/>. Gum's cursor hit-testing reads <c>Zoom</c> and
    /// <c>ClientWidth</c>/<c>ClientHeight</c>/<c>ClientLeft</c>/<c>ClientTop</c> off this object, so
    /// all four must track the real viewport — not just <c>Zoom</c> — or hit-testing drifts from the
    /// rendered position whenever window size != design resolution (issue #824).
    /// </summary>
    internal void SyncCursorCamera(RenderingLibrary.Camera gumCamera, Camera camera)
    {
        gumCamera.Zoom = ResolveZoom(camera);
        gumCamera.ClientWidth = camera.Viewport.Width;
        gumCamera.ClientHeight = camera.Viewport.Height;
        gumCamera.ClientLeft = camera.Viewport.X;
        gumCamera.ClientTop = camera.Viewport.Y;
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
