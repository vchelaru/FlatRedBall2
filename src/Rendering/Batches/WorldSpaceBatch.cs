using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

public class WorldSpaceBatch : IRenderBatch
{
    public static readonly WorldSpaceBatch Instance = new WorldSpaceBatch();

    /// <inheritdoc/>
    public bool FlipsY => true;

    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => spriteBatch.Begin(
            transformMatrix: camera.GetTransformMatrix(),
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);

    public void End(SpriteBatch spriteBatch) => spriteBatch.End();
}
