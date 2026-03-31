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
public class GumRenderable : IRenderable, IAttachable
{
    /// <summary>The root Gum element rendered by this object.</summary>
    public GraphicalUiElement Visual { get; }

    /// <param name="visual">
    /// The Gum visual to render. Pass the <c>.Visual</c> property of a Forms control
    /// (e.g. <c>button.Visual</c>), a raw <c>ContainerRuntime</c>, or any
    /// <see cref="GraphicalUiElement"/>.
    /// </param>
    public GumRenderable(GraphicalUiElement visual)
    {
        Visual = visual;
    }

    // IAttachable
    /// <summary>
    /// When set, the visual is positioned in world space at this entity's location each frame.
    /// <c>AbsoluteX/Y</c> are converted through the camera to screen pixels before drawing.
    /// Leave null (default) for screen-space rendering.
    /// </summary>
    public Entity? Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;
    public void Destroy() { } // lifecycle managed through Screen

    // IRenderable
    public float Z { get; set; }
    public Layer? Layer { get; set; }
    public IRenderBatch Batch { get; set; } = GumRenderBatch.Instance;
    public string? Name { get; set; }

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!Visual.Visible) return;
        if (Parent != null)
        {
            var screenPos = camera.WorldToScreen(
                new System.Numerics.Vector2(AbsoluteX, AbsoluteY));
            Visual.X = screenPos.X;
            Visual.Y = screenPos.Y;
        }
        GumRenderBatch.Instance.DrawElement(Visual);
    }
}
