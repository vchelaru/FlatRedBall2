using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Wraps a configured <c>SpriteBatch.Begin</c>/<c>End</c> pair (transform, sampler, blend, etc.).
/// The engine groups consecutive <see cref="IRenderable"/>s that share the same batch instance
/// into one <c>Begin</c>/<c>End</c> call, so prefer reusing singleton batches
/// (e.g. <c>WorldSpaceBatch.Instance</c>) over allocating per-renderable.
/// </summary>
public interface IRenderBatch
{
    /// <summary>
    /// True when this batch's transform includes a Y-axis flip (world Y+ up → screen Y+ down).
    /// <see cref="Sprite.Draw"/> uses this to apply <c>SpriteEffects.FlipVertically</c> so
    /// texture pixels appear right-side-up after the transform.
    /// </summary>
    bool FlipsY { get; }

    /// <summary>
    /// Called by the engine before any renderable in this batch draws. Implementations call
    /// <c>spriteBatch.Begin(...)</c> with the appropriate transform, sampler, blend, and sort settings.
    /// </summary>
    void Begin(SpriteBatch spriteBatch, Camera camera);

    /// <summary>
    /// Called by the engine after the last renderable in this batch has drawn. Implementations
    /// call <c>spriteBatch.End()</c>.
    /// </summary>
    void End(SpriteBatch spriteBatch);
}
