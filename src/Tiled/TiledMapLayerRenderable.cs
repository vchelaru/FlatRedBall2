using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Tiled;

// TODO: Implement Tiled integration. Add MonoGame.Extended.Tiled NuGet and render tile layers.
// See design/TODOS.md
public class TiledMapLayerRenderable : IRenderable, IAttachable
{
    // TODO: Accept TiledMap and TiledMapTileLayer parameters
    public TiledMapLayerRenderable() { }

    // IAttachable
    public Entity? Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;
    public void Destroy() { }

    // IRenderable
    public float Z { get; set; }
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
    public string? Name { get; set; }

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        // TODO: Render tile layer using MonoGame.Extended.Tiled renderer
    }
}
