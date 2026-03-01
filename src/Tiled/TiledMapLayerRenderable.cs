using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Tiled;

// TODO: Implement Tiled integration. Add MonoGame.Extended.Tiled NuGet and render tile layers.
// See design/TODOS.md
public class TiledMapLayerRenderable : IRenderable
{
    // TODO: Accept TiledMap and TiledMapTileLayer parameters
    public TiledMapLayerRenderable() { }

    public float Z { get; set; }
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
    public string? Name { get; set; }

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        // TODO: Render tile layer using MonoGame.Extended.Tiled renderer
    }
}
