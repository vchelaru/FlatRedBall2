using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using Gum.Wireframe;

namespace FlatRedBall2.UI;

/// <summary>
/// Wraps a Gum <see cref="GraphicalUiElement"/> as an <see cref="IRenderable"/> so it can be
/// sorted by Layer and Z alongside sprites and shapes in the Screen's render list.
/// </summary>
/// <remarks>
/// This type is an internal implementation detail. Use <c>screen.Add(element)</c> for screen-space
/// elements or <c>entity.Add(element)</c> for world-space elements — both handle wrapping internally.
/// </remarks>
public class GumRenderable : IRenderable
{
    /// <summary>The root Gum element rendered by this object.</summary>
    public GraphicalUiElement Visual { get; }

    /// <summary>
    /// When set, the visual is positioned in world space at this entity's location each frame.
    /// <c>AbsoluteX/Y</c> are converted through the camera to screen pixels before drawing.
    /// Leave null (default) for screen-space rendering.
    /// </summary>
    public Entity? WorldParent { get; set; }

    /// <param name="visual">
    /// The Gum visual to render. Pass the <c>.Visual</c> property of a Forms control
    /// (e.g. <c>button.Visual</c>), a raw <c>ContainerRuntime</c>, or any
    /// <see cref="GraphicalUiElement"/>.
    /// </param>
    public GumRenderable(GraphicalUiElement visual)
    {
        Visual = visual;
    }

    // IRenderable
    public float Z { get; set; }
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = GumRenderBatch.Instance;
    public string? Name { get; set; }

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (WorldParent != null)
        {
            var screenPos = camera.WorldToScreen(
                new System.Numerics.Vector2(WorldParent.AbsoluteX, WorldParent.AbsoluteY));
            Visual.X = screenPos.X;
            Visual.Y = screenPos.Y;
        }
        GumRenderBatch.Instance.DrawElement(Visual);
    }
}
