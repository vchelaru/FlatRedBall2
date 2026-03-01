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
    public static readonly GumRenderBatch Instance = new GumRenderBatch();

    private NativeGumBatch? _inner;

    /// <summary>
    /// Creates the inner <c>RenderingLibrary.Graphics.GumBatch</c>.
    /// Must be called after <c>GumService.Default.Initialize</c>.
    /// Called automatically by <see cref="FlatRedBallService.Initialize"/>.
    /// </summary>
    internal void Initialize()
    {
        _inner = new NativeGumBatch();
    }

    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
    {
        _inner!.Begin();
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
