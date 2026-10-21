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
    private readonly List<GumRenderable> _renderables = new();
    private readonly System.Action<GumRenderBatch, Camera, List<GraphicalUiElement>> _preRender;

    /// <param name="preRender">Test seam replacing the Gum <c>PreRenderRoots</c> call; null uses Gum.</param>
    internal GumRenderTargetBaker(System.Action<GumRenderBatch, Camera, List<GraphicalUiElement>>? preRender = null)
    {
        _preRender = preRender ?? PreRenderWithGum;
    }

    /// <summary>Bakes every render target under <paramref name="renderables"/> as seen by <paramref name="camera"/>.</summary>
    internal void Bake(IEnumerable<GumRenderable> renderables, Camera camera)
    {
        // Enumerated once: the caller's sequence is a lazy filter over the whole render list.
        _renderables.Clear();
        _renderables.AddRange(renderables);

        // The two batch kinds differ in the zoom they draw at, so each is baked at its own zoom. Roots
        // sharing a batch go in one call so a sprite can reference a source container in another root.
        Bake(camera, GumRenderBatch.Instance);
        Bake(camera, GumRenderBatch.ScreenSpaceInstance);
        _renderables.Clear();
    }

    /// <summary>
    /// True when any visible node under <paramref name="roots"/> is, or points at, a render target.
    /// Mirrors what Gum's <c>PreRenderRoots</c> looks for, but never descends into an invisible subtree.
    /// An invisible render-target container counts: Gum bakes it when a visible sprite references it.
    /// </summary>
    internal static bool ContainsRenderTarget(IEnumerable<GraphicalUiElement> roots)
    {
        foreach (var root in roots)
        {
            if (ContainsRenderTarget(root))
                return true;
        }
        return false;
    }

    private static bool ContainsRenderTarget(IRenderableIpso node)
    {
        if (node.IsRenderTarget)
            return true;
        if (!node.Visible)
            return false;
        if (node is IRenderTargetTextureReferencer referencer && referencer.RenderTargetTextureSource != null)
            return true;

        var children = node.Children;
        if (children == null)
            return false;
        for (int i = 0; i < children.Count; i++)
        {
            if (ContainsRenderTarget(children[i]))
                return true;
        }
        return false;
    }

    private void Bake(Camera camera, GumRenderBatch batch)
    {
        _roots.Clear();
        foreach (var renderable in _renderables)
        {
            if (renderable.Batch == batch)
                _roots.Add(renderable.Visual);
        }
        // Gum's Draw already runs the per-node PreRender for everything it draws, so with no render
        // target in the roots the bake has nothing to do. Skipping it avoids walking the whole tree
        // (twice inside PreRenderRoots) on every frame of a scene that never uses render targets.
        if (_roots.Count == 0 || !ContainsRenderTarget(_roots))
        {
            _roots.Clear();
            return;
        }

        // Entity-attached visuals are repositioned in Draw otherwise, after the bake would have run.
        foreach (var renderable in _renderables)
        {
            if (renderable.Batch == batch)
                renderable.PositionForCamera(camera);
        }

        _preRender(batch, camera, _roots);
        _roots.Clear();
    }

    private static void PreRenderWithGum(GumRenderBatch batch, Camera camera, List<GraphicalUiElement> roots)
    {
        var renderer = RenderingLibrary.SystemManagers.Default?.Renderer;
        if (renderer == null) return;

        // Bake at the zoom and viewport the composite will use.
        batch.SyncCursorCamera(renderer.Camera, camera);
        renderer.PreRenderRoots(roots);
    }
}
