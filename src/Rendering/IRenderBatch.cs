using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering;

public interface IRenderBatch
{
    void Begin(SpriteBatch spriteBatch, Camera camera);
    void End(SpriteBatch spriteBatch);
}
