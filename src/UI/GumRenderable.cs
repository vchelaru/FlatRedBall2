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

    /// <summary>
    /// When non-null, this renderable belongs to a specific camera's HUD and is drawn only on
    /// that camera's pass. When null, it is either world-space (parented via <see cref="Parent"/>)
    /// or — combined with <see cref="IsOverlay"/> — a screen-level overlay drawn after the camera loop.
    /// </summary>
    internal Camera? OwningCamera { get; set; }

    /// <summary>When true, this renderable is drawn only in the post-camera-loop overlay pass.</summary>
    internal bool IsOverlay { get; set; }

    /// <summary>
    /// True when this renderable should draw for <paramref name="activeCamera"/>'s pass:
    /// world-space (no owner, not overlay), or HUD owned by exactly this camera.
    /// Overlay renderables always return false — they are handled by the post-camera pass.
    /// </summary>
    internal bool ShouldDrawForCamera(Camera activeCamera)
    {
        if (IsOverlay) return false;
        if (OwningCamera == null) return true;
        return ReferenceEquals(OwningCamera, activeCamera);
    }

    // IAttachable
    /// <summary>
    /// When set, the visual is positioned in world space at this entity's location each frame.
    /// <c>AbsoluteX/Y</c> are converted through the camera to screen pixels before drawing.
    /// Leave null (default) for screen-space rendering.
    /// </summary>
    public Entity? Parent { get; set; }
    /// <inheritdoc/>
    public float X { get; set; }
    /// <inheritdoc/>
    public float Y { get; set; }
    /// <inheritdoc/>
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    /// <inheritdoc/>
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    /// <inheritdoc/>
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;
    /// <inheritdoc/>
    public void Destroy() { } // lifecycle managed through Screen

    // IRenderable
    /// <inheritdoc/>
    public float Z { get; set; }
    /// <inheritdoc/>
    public Layer? Layer { get; set; }
    /// <inheritdoc/>
    public IRenderBatch Batch { get; set; } = GumRenderBatch.Instance;
    /// <inheritdoc/>
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
