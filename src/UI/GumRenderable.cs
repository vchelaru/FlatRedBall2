using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using Gum.Wireframe;

namespace FlatRedBall2.UI;

/// <summary>
/// Wraps a Gum <see cref="GraphicalUiElement"/> as an <see cref="IRenderable"/> so it can be
/// sorted by Layer and Z alongside sprites and shapes in the Screen's render list.
/// Renders in screen space by default.
/// </summary>
/// <remarks>
/// Add to a Screen via <see cref="Screen.AddGum(GumRenderable)"/>, which registers it for both
/// rendering and input updates (click, hover, etc. on Forms controls).
/// <para>
/// To build up UI, add child controls to <see cref="Visual"/>:
/// <code>
/// var button = new Button();
/// var hud = new GumRenderable(button.Visual);
/// AddGum(hud);
/// </code>
/// Or wrap a container that holds multiple controls:
/// <code>
/// var panel = new StackPanel();
/// panel.AddChild(new Button());
/// var hud = new GumRenderable(panel.Visual);
/// AddGum(hud);
/// </code>
/// </para>
/// <para>
/// TODO: World-space attachment — if a future Parent entity is set, project
/// AbsoluteX/Y through <c>camera.WorldToScreen</c> to offset <see cref="Visual"/>'s
/// position before drawing.
/// </para>
/// </remarks>
public class GumRenderable : IRenderable
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

    // IRenderable
    public float Z { get; set; }
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = GumRenderBatch.Instance;
    public string? Name { get; set; }

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        GumRenderBatch.Instance.DrawElement(Visual);
    }
}
