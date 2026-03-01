using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Gum;

// TODO: Implement Gum integration. Wrap a Gum screen/component as an IRenderable.
// See design/TODOS.md
public class GumRenderable : IRenderable
{
    public GumRenderable()
    {
        // TODO: Accept a Gum screen/component reference
    }

    public float Z { get; set; }
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = GumBatch.Instance;
    public string? Name { get; set; }

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        // TODO: Call Gum's draw method
    }
}
