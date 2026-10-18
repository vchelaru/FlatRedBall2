using System.Collections.Generic;
using FlatRedBall2.Rendering;
using Gum.Wireframe;
using RenderingLibrary.Graphics;

namespace FlatRedBall2.UI;

/// <summary>
/// Bakes Gum <c>IsRenderTarget</c> containers before a draw pass. The <c>GumBatch</c> path
/// <see cref="GumRenderBatch"/> draws through never bakes them, so a render-target container
/// composites empty and a <c>Sprite.RenderTargetTextureSource</c> samples nothing. Gum's
/// <c>Renderer.PreRenderRoots</c> is the host hook for this and must run outside any open batch.
/// </summary>
internal sealed class GumRenderTargetBaker
{
    private readonly List<GraphicalUiElement> _roots = new();

    /// <summary>Bakes every render target under <paramref name="renderables"/> as seen by <paramref name="camera"/>.</summary>
    internal void Bake(IEnumerable<GumRenderable> renderables, Camera camera)
    {
        var renderer = RenderingLibrary.SystemManagers.Default?.Renderer;
        if (renderer == null) return;

        // The two batch kinds differ in the zoom they draw at, so each is baked at its own zoom. Roots
        // sharing a batch go in one call so a sprite can reference a source container in another root.
        Bake(renderer, renderables, camera, GumRenderBatch.Instance);
        Bake(renderer, renderables, camera, GumRenderBatch.ScreenSpaceInstance);
    }

    private void Bake(Renderer renderer, IEnumerable<GumRenderable> renderables, Camera camera, GumRenderBatch batch)
    {
        _roots.Clear();
        foreach (var renderable in renderables)
        {
            if (renderable.Batch != batch) continue;
            // Entity-attached visuals are repositioned in Draw otherwise, after the bake would have run.
            renderable.PositionForCamera(camera);
            _roots.Add(renderable.Visual);
        }
        if (_roots.Count == 0) return;

        // Bake at the zoom and viewport the composite will use.
        batch.SyncCursorCamera(renderer.Camera, camera);
        renderer.PreRenderRoots(_roots);
        _roots.Clear();
    }
}
