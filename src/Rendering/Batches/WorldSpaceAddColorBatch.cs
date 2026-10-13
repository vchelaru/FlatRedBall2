using FlatRedBall2;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

/// <summary>
/// Same camera transform/sampler/culling as <see cref="WorldSpaceBatch"/>, but draws through the
/// add-color pixel shader (<see cref="AddColorEffect"/>) instead of the default <c>SpriteEffect</c>.
/// <see cref="Sprite"/> swaps to this batch automatically while its <see cref="Sprite.AddColor"/>
/// is non-zero (including when an <see cref="Animation.ColorOperation.Add"/> frame assigned it) —
/// see <see cref="Sprite.Batch"/>.
/// <para>
/// Uses <see cref="SpriteSortMode.Immediate"/> so each sprite's <c>ColorOffset</c> parameter
/// (set from <see cref="Sprite.AddColor"/> by <see cref="Sprite.Draw"/> immediately before its draw call) takes effect per-sprite
/// rather than being overwritten by the last sprite drawn before the batch flushes.
/// </para>
/// </summary>
public class WorldSpaceAddColorBatch : IRenderBatch
{
    /// <summary>The shared singleton instance.</summary>
    public static readonly WorldSpaceAddColorBatch Instance = new WorldSpaceAddColorBatch();

    /// <inheritdoc/>
    public bool FlipsY => true;

    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => spriteBatch.Begin(
            sortMode: SpriteSortMode.Immediate,
            transformMatrix: camera.GetTransformMatrix(),
            samplerState: FlatRedBallService.Default.DisplaySettings.GetSamplerState(),
            rasterizerState: RasterizerState.CullNone,
            effect: AddColorEffect.Get(spriteBatch.GraphicsDevice));

    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch) => spriteBatch.End();
}
