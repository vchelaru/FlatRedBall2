using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

/// <summary>
/// A render batch that draws elements in world space, applying camera transformations.
/// </summary>
public class WorldSpaceBatch : IRenderBatch
{
    /// <summary>The shared singleton instance.</summary>
    public static readonly WorldSpaceBatch Instance = new WorldSpaceBatch();

    /// <inheritdoc/>
    public bool FlipsY => true;

    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => spriteBatch.Begin(
            transformMatrix: camera.GetTransformMatrix(),
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);

    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch) => spriteBatch.End();
}
