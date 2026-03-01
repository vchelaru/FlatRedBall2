using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

public class ScreenSpaceBatch : IRenderBatch
{
    public static readonly ScreenSpaceBatch Instance = new ScreenSpaceBatch();

    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => spriteBatch.Begin();

    public void End(SpriteBatch spriteBatch) => spriteBatch.End();
}
