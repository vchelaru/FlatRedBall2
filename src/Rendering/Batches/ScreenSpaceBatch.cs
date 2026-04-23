using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

/// <summary>
/// A render batch that draws elements in screen space (e.g., UI, overlays) independent of camera position.
/// </summary>
public class ScreenSpaceBatch : IRenderBatch
{
    /// <summary>The shared singleton instance.</summary>
    public static readonly ScreenSpaceBatch Instance = new ScreenSpaceBatch();

    /// <inheritdoc/>
    public bool FlipsY => false;

    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => spriteBatch.Begin(samplerState: SamplerState.PointClamp);

    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch) => spriteBatch.End();
}
