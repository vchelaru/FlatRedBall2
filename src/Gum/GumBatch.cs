using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Gum;

// TODO: Implement Gum integration. Add Gum NuGet package and wire up Gum's render pass.
// See design/TODOS.md
public class GumBatch : IRenderBatch
{
    public static readonly GumBatch Instance = new GumBatch();

    public void Begin(SpriteBatch spriteBatch, Camera camera)
    {
        // TODO: Initialize Gum's render pass
    }

    public void End(SpriteBatch spriteBatch)
    {
        // TODO: Finalize Gum's render pass
    }
}
