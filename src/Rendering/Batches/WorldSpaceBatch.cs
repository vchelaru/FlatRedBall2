using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

public class WorldSpaceBatch : IRenderBatch
{
    public static readonly WorldSpaceBatch Instance = new WorldSpaceBatch();

    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => spriteBatch.Begin(transformMatrix: camera.GetTransformMatrix());

    public void End(SpriteBatch spriteBatch) => spriteBatch.End();
}
