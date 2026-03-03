using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering;

public interface IRenderBatch
{
    /// <summary>
    /// True when this batch's transform includes a Y-axis flip (world Y+ up → screen Y+ down).
    /// <see cref="Sprite.Draw"/> uses this to apply <c>SpriteEffects.FlipVertically</c> so
    /// texture pixels appear right-side-up after the transform.
    /// </summary>
    bool FlipsY { get; }

    void Begin(SpriteBatch spriteBatch, Camera camera);
    void End(SpriteBatch spriteBatch);
}
