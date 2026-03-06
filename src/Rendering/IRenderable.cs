using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering;

public interface IRenderable
{
    float Z { get; }
    Layer Layer { get; set; }
    IRenderBatch Batch { get; }
    string? Name { get; }
    void Draw(SpriteBatch spriteBatch, Camera camera);
}
